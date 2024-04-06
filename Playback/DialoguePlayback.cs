using Godot;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using SadChromaLib.Types;
using SadChromaLib.Specialisations.Dialogue.Types;

namespace SadChromaLib.Specialisations.Dialogue.Playback;

/// <summary>
/// A UI-agnostic implementation for playing back dialogue.
/// </summary>
public sealed partial class DialoguePlayback
{
	public event Action<string> OnDialoguePrint;

	public IDialoguePlaybackHandler PlaybackHandler;

	DialogueGraph _dialogueGraphRef;

	bool _hasRanCommands;
	int _currentIdx;

	readonly Dictionary<string, AnyData> _scriptVariables;

	#region Constructors

	public DialoguePlayback(IDialoguePlaybackHandler playbackHandler) {
		_scriptVariables = new();

		_hasRanCommands = false;
		_currentIdx = 0;

		PlaybackHandler = playbackHandler;
	}

	public DialoguePlayback(DialogueGraph graph, IDialoguePlaybackHandler playbackHandler)
		: this(playbackHandler)
	{
		SetData(graph);

		// Default print handler
		OnDialoguePrint += (string text) => GD.Print("Dialogue Print: ", text);
	}

	public DialoguePlayback(string graphPath, IDialoguePlaybackHandler playbackHandler)
		: this(playbackHandler)
	{
		if (!DialogueParser.Load(graphPath, out DialogueGraph graph))
		{
			GD.PrintErr($"DialoguePlayback: Failed to load dialogue graph at \"{graphPath}\"");
			return;
		}

		SetData(graph);
	}

	#endregion

    #region Main Functions

	public bool HasData() {
		return _dialogueGraphRef is not null;
	}

	/// <summary>
    /// Sets the dialogue playback's dialogue graph (Use this before calling anything!)
    /// </summary>
    /// <param name="graphRef">The graph to switch to.</param>
    public void SetData(DialogueGraph graphRef)
	{
		_dialogueGraphRef = graphRef;
		Reset();
	}

	public DialogueNode GetCurrentBlock() {
		return _dialogueGraphRef.Nodes[_currentIdx];
	}

	public bool GetCurrentCommands(ref ReadOnlySpan<DialogueCommand> commands) {
		commands = _dialogueGraphRef.Nodes[_currentIdx].CommandList.AsSpan();
		return commands.Length > 0;
	}

	public bool GetCurrentChoices(ref ReadOnlySpan<DialogueChoice> choices) {
		choices = _dialogueGraphRef.Nodes[_currentIdx].Choices;
		return choices.Length > 0;
	}

	public bool CurrentBlockHasChoices() {
		return (_dialogueGraphRef.Nodes[_currentIdx].Choices?.Length ?? 0) > 0;
	}

	private void SetCurrentBlock(int idx)
	{
		_currentIdx = idx;

		if (idx < 0 || idx >= _dialogueGraphRef.Nodes.Length)
			return;

		DialogueNode block = GetCurrentBlock();

		// Automatically execute commands if a choice block is coming up
		if (CurrentBlockHasChoices() && HandleCommands())
			return;

		_hasRanCommands = false;

		PlaybackHandler?.OnPlaybackPresentDialogue(
			character: ResolveVariables(block.CharacterId),
			dialogue: ResolveVariables(block.DialogueText)
		);

		PlaybackHandler?.OnPlaybackPresentChoices(block.Choices);
	}

	/// <summary>
	/// Advances the script until a choice needs to made.
	/// </summary>
	public void Next()
	{
		if (HandleCommands())
			return;

		DialogueNode block = GetCurrentBlock();

		// Prevent advancing the dialogue if the player is expected to make a choice
		if (block.Choices?.Length > 0)
			return;

		_currentIdx ++;

		if (_currentIdx >= _dialogueGraphRef.Nodes.Length) {
			PlaybackHandler?.OnPlaybackCompleted(this);
			return;
		}

		SetCurrentBlock(_currentIdx);
	}

	/// <summary>
	/// Jumps to the dialogue block specified by the target choice
	/// </summary>
	/// <param name="choiceIdx">The index of the choice to use</param>
	public void SelectChoice(int choiceIdx)
	{
		ReadOnlySpan<DialogueChoice> choices = default;

		if (!GetCurrentChoices(ref choices))
			return;

		Jump(choices[choiceIdx].TargetTag);
	}

	/// <summary>
	/// Jumps to a specific block in the script
	/// </summary>
	/// <param name="index">The index to jump to.</param>
	public void Jump(int index)
	{
		Debug.Assert(
			condition: index >= 0 && index < _dialogueGraphRef.Nodes.Length,
			message: "DialoguePlayback.Jump: Invalid jump index."
		);

		SetCurrentBlock(index);
	}

	/// <summary>
	/// Jumps to a specific block in the script
	/// </summary>
	/// <param name="tag">The unique tag to jump to.</param>
	public void Jump(string tag)
	{
		int? blockIdx = _dialogueGraphRef.FindIndex(tag);

		if (blockIdx == null)
			return;

		SetCurrentBlock(blockIdx.Value);
	}

	/// <summary>
	/// Stops the playback and resets its index to zero.
	/// </summary>
	public void Stop()
	{
		PlaybackHandler?.OnPlaybackCompleted(this);
		Reset();
	}

	/// <summary>
	/// Resets the state of the playback.
	/// </summary>
	public void Reset()
	{
		_currentIdx = 0;
	}

	/// <summary>
	/// Resets the state of the playback and its variables.
	/// </summary>
	public void ResetFull()
	{
		_currentIdx = 0;
		_scriptVariables.Clear();
	}

	/// <summary>
	/// Re-runs the currently-active block
	/// </summary>
	public void ReloadBlock() {
		SetCurrentBlock(_currentIdx);
	}

	#endregion

	#region Commands

	private bool HandleCommands()
	{
		if (_hasRanCommands)
			return false;

		ReadOnlySpan<DialogueCommand> commands = default;

		if (!GetCurrentCommands(ref commands)) {
			return false;
		}

		for (int i = 0; i < commands.Length; ++ i) {
			if (EvaluateDefaultCommands(commands[i]))
				return true;

			PlaybackHandler?.OnPlaybackEvaluateCommand(this, commands[i]);
		}

		_hasRanCommands = true;
		return false;
	}

	private bool EvaluateDefaultCommands(DialogueCommand command)
	{
		switch (command.Name)
		{
			case "close":
				Stop();
				return true;

			case "closeif":
				if (command.Parameters?.Length < 1) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@closeif'",
						"\nUsage: @closeif <requisite variable name>");
					break;
				}

				if (!_scriptVariables.ContainsKey(command.Parameters[0]))
					break;

				Stop();
				return true;

			case "jump":
				if (command.Parameters?.Length < 1) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@jump'",
						"\nUsage: @jump <target tag>");
					break;
				}

				Jump(command.Parameters[0]);
				return true;

			case "jumpif":
				if (command.Parameters?.Length < 2) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@jumpif'",
						"\nUsage: @jumpif <target tag> <requisite variable name>");
					break;
				}

				if (!_scriptVariables.ContainsKey(command.Parameters[1]))
					break;

				Jump(command.Parameters[0]);
				return true;

			case "set":
				if (command.Parameters?.Length < 2) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@set'",
						"\nUsage: @set <variable name> <value> ...");
					break;
				}

				string value = command.Parameters[1..].Join(" ");

				// The command name set only supports float and string
				if (!float.TryParse(value, out float f)) {
					SetVariable(command.Parameters[0], f);
				}
				else {
					SetVariable(command.Parameters[0], value);
				}

				break;

			case "flag":
				if (command.Parameters?.Length < 1) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@flag'",
						"\nUsage: @flag <variable name>");
					break;
				}

				SetVariable(command.Parameters[0], true);
				break;

			case "print":
				if (command.Parameters?.Length < 1) {
					GD.PrintErr(
						"Dialogue script: invalid use of command '@print'",
						"\nUsage: @print <text> ...");
					break;
				}

				OnDialoguePrint?.Invoke(command.Parameters[1..].Join(" "));
				break;
		}

		return false;
	}

	#endregion

	#region Variable Organisation

	/// <summary>
	/// Replaces variable names in a text with their actual values.
	/// </summary>
	/// <param name="text">The text to parse.</param>
	/// <returns></returns>
	public string ResolveVariables(string text)
		=> DialogueParser.ParseAndResolveVariables(text, ResolveVariablesCallback);

	/// <summary>
	/// Sets/Updates a variable for the playback instance
	/// </summary>
	/// <param name="name">The name of the variable to set</param>
	/// <param name="value">Its value</param>
	public void SetVariable(string name, AnyData value)
	{
		_scriptVariables[name] = value;
		ReloadBlock();
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public bool GetVarBool(string name, bool @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Bool)
			return @default;

		return _scriptVariables[name].BoolValue;
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public int GetVarInt(string name, int @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Int)
			return @default;

		return _scriptVariables[name].IntValue;
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public float GetVarFloat(string name, float @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Float)
			return @default;

		return _scriptVariables[name].X;
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public Vector2 GetVarVec2(string name, Vector2 @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Vector2)
			return @default;

		return _scriptVariables[name].AsV2();
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public Vector3 GetVarVec3(string name, Vector3 @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Vector3)
			return @default;

		return _scriptVariables[name].AsV3();
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public Color GetVarColour(string name, Color @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.Colour)
			return @default;

		return _scriptVariables[name].AsColour();
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <returns></returns>
	public string GetVarText(string name, string @default = default)
	{
		if (!_scriptVariables.ContainsKey(name) &&
			_scriptVariables[name].DataType == AnyData.Type.String)
			return @default;

		return _scriptVariables[name].Text;
	}

	/// <summary>
	/// Removes a variable from the current playback instance.
	/// </summary>
	/// <param name="name">The name of the variable remove.</param>
	public void RemoveVariable(string name)
	{
		if (!_scriptVariables.ContainsKey(name))
			return;

		_scriptVariables.Remove(name);
	}

	#endregion

	#region Utils

	private string ResolveVariablesCallback(string variableName)
	{
		if (!_scriptVariables.TryGetValue(variableName, out AnyData value)) {
			return variableName;
		}

		return value.ToString();
	}

	#endregion
}
