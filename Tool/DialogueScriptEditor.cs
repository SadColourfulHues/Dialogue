using Godot;

using System;

namespace SadChromaLib.Dialogue.Editor;

public partial class DialogueScriptEditor : CodeEdit
{
	private const string KeyText = "insert_text";

	private string[] CommandNames = {
		"jump",
		"set",
		"flag",
		"close",
		"closeif",
		"jumpif"
	};

	public override void _Ready()
	{
		SyntaxHighlighter = new DialogueScriptSyntaxHighlighter();

		CodeCompletionEnabled = true;
		CodeCompletionPrefixes = new() {
			"@"
		};
	}

	public override void _ConfirmCodeCompletion(bool replace)
	{
		ReadOnlySpan<char> line = GetLine(GetCaretLine());

		DialogueParser.Type type = DialogueParser.GetLineType(line);

		if (type != DialogueParser.Type.Command)
			return;

		DialogueParser.CommandInfo commandInfo = DialogueParser.ParseCommand(line);
		line = commandInfo.Name;

		if (line.Length < 1) {
			InsertTextAtCaret(line.ToString());
			CancelCodeCompletion();
			return;
		}

		int completionIdx = GetCodeCompletionSelectedIndex();

		ReadOnlySpan<char> completion = (string) GetCodeCompletionOption(completionIdx)[KeyText];

		for (int i = 0; i < line.Length; ++ i) {
			if (i < completion.Length &&
				line[i] == completion[i])
			{
				completionIdx = i;
				continue;
			}

			break;
		}

		InsertTextAtCaret(completion[completionIdx..].ToString());
		CancelCodeCompletion();
	}

	public override void _RequestCodeCompletion(bool force)
	{
		ReadOnlySpan<char> line = GetLine(GetCaretLine());

		DialogueParser.Type type = DialogueParser.GetLineType(line);

		if (type != DialogueParser.Type.Command)
			return;

		DialogueParser.CommandInfo command = DialogueParser.ParseCommand(line);
		line = command.Name;

		// Try to find matches based on the command name
		bool isBlankCommand = line.Length < 1;

		for (int j = 0; j < CommandNames.Length; ++ j) {
			ReadOnlySpan<char> candidate = CommandNames[j];
			int? partialIndex = null;

			if (!isBlankCommand &&
				!CheckMatch(line, candidate, ref partialIndex))
			{
					continue;
			}

			string candidateStr = candidate.ToString();

			AddCodeCompletionOption(
				type: CodeCompletionKind.Function,
				displayText: candidateStr,
				insertText: partialIndex != null
					? candidate[partialIndex.Value..].ToString()
					: candidateStr
			);
		}

		UpdateCodeCompletionOptions(true);
	}

	private static bool CheckMatch(
		ReadOnlySpan<char> line,
		ReadOnlySpan<char> candidate,
		ref int? partialIndex)
	{
		if (line.Length < candidate.Length)
			return false;

		for (int k = 0; k < candidate.Length; ++ k) {
			if (line[k] == candidate[k])
				continue;

			partialIndex = k;
			break;
		}

		return partialIndex != null;
	}
}
