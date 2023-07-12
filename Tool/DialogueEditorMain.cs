using Godot;
using System;

namespace SadChromaLib.Dialogue.Editor;

public sealed partial class DialogueEditorMain : Control
{
	private const string TitleSaveWarning = "Maybe you should save?";
	private const string MessageNew = "This will create a new script file. Make sure to save your work if you don't want to lose them before continuing.";
	private const string MessageOpen = "This will replace the script file you're currently working on. Make sure to save if you don't want to lose your progress.";

	private string[] ScriptFileFilter = {
		"*.txt ; Dialogue Script Files, Text Files"
	};

	private string[] GraphFileFilter = {
		"*.tres ; Dialogue Graph Files, Godot Resource Files"
	};

	private readonly DialogueParser _parser;
	private CodeEdit _scriptEditor;

	private string _lastFilePath;

	public DialogueEditorMain()
	{
		_parser = new();
	}

	public override void _Ready()
	{
		_scriptEditor = GetNode<CodeEdit>("%Script");

		GetNode<Button>("%New").Pressed += OnNewPressed;
		GetNode<Button>("%Load").Pressed += OnLoadPressed;
		GetNode<Button>("%Save").Pressed += OnSavePressed;
		GetNode<Button>("%Export").Pressed += OnExportPressed;
	}

	#region Events

	private void OnNewPressed()
	{
		StartPrompt(
			title: TitleSaveWarning,
			message: MessageNew,
			callback: () => {
				_lastFilePath = null;
				_scriptEditor.Clear();
			}
		);
	}

	private void OnLoadPressed()
	{
		StartFileDialog(
			title: "Open Dialogue Script",
			readMode: true,
			fileFilter: ScriptFileFilter,
			callback: (string filePath)
			=> StartPrompt(
				title: TitleSaveWarning,
				message: MessageOpen,
				callback: () => ReadFileFromDisk(filePath)
			)
		);
	}

	private void OnSavePressed()
	{
		if (_lastFilePath != null) {
			WriteFileToDisk(_lastFilePath);
			return;
		}

		StartFileDialog(
			title: "Save Dialogue Script",
			readMode: false,
			fileFilter: ScriptFileFilter,
			callback: WriteFileToDisk
		);
	}

	private void OnExportPressed()
	{
		StartFileDialog(
			title: "Export Dialogue Graph",
			readMode: false,
			fileFilter: GraphFileFilter,
			callback: CompileAndWriteGraphToDisk
		);
	}

	#endregion

	#region I/O

	private void ReadFileFromDisk(string filePath)
	{
		_lastFilePath = filePath;

		FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		string contents = file.GetAsText();
		file.Close();

		_scriptEditor.Text = contents;
	}

	private void WriteFileToDisk(string filePath)
	{
		_lastFilePath = filePath;

		FileAccess file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
		file.StoreString(_scriptEditor.Text);
		file.Close();
	}

	private void CompileAndWriteGraphToDisk(string filePath)
	{
		DialogueGraph graph = _parser.Compile(_scriptEditor.Text);
		ResourceSaver.Save(graph, filePath);
	}

	#endregion

	#region Helpers

	private void StartFileDialog(
		string title,
		bool readMode,
		string[] fileFilter,
		Action<string> callback)
	{
		FileDialog dialogRef = new() {
			Access = FileDialog.AccessEnum.Resources,
			FileMode = readMode ? FileDialog.FileModeEnum.OpenFile : FileDialog.FileModeEnum.SaveFile,
			Title = title,
			Filters = fileFilter
		};

		AddChild(dialogRef);

		dialogRef.FileSelected += (string filePath) => {
			dialogRef.QueueFree();
			callback.Invoke(filePath);
		};

		dialogRef.Canceled += dialogRef.QueueFree;

		dialogRef.PopupCentered(new(300,420));
	}

	private void StartPrompt(
		string title,
		string message,
		Action callback)
	{
		ConfirmationDialog dialogRef = new() {
			Title = title,
			DialogText = message
		};

		AddChild(dialogRef);

		dialogRef.Confirmed += () => {
			dialogRef.QueueFree();
			callback.Invoke();
		};

		dialogRef.CloseRequested += dialogRef.QueueFree;
		dialogRef.Canceled += dialogRef.QueueFree;

		dialogRef.PopupCentered(new(250, 64));
	}

	#endregion
}
