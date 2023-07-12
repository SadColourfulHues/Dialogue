using Godot;
using Godot.Collections;

using System;
using System.Diagnostics;

using SadChromaLib.Dialogue.Nodes;

namespace SadChromaLib.Dialogue;

/// <summary>
/// A UI-agnostic implementation for playing back dialogue.
/// </summary>
[GlobalClass]
public sealed partial class DialoguePlayback : Node
{
	private const string QuitCommand = "close";

	#region Signals

	[Signal]
	public delegate void CharacterUpdatedEventHandler(StringName characterId);

	[Signal]
	public delegate void DialogueTextUpdatedEventHandler(string dialogue);

	[Signal]
	public delegate void ChoicesUpdatedEventHandler(DialogueChoice[] choices);

	[Signal]
	public delegate void HandleCommandRequestEventHandler(DialogueNodeCommand command);

	[Signal]
	public delegate void PlaybackCompletedEventHandler();

	#endregion

	[Export]
	private DialogueGraph _dialogueGraphRef;

	private DialogueNode _currentBlock;

	private Dictionary<StringName, Variant> _scriptVariables;
	private DialogueNodeCommand[] _nextCommands;

	private int _count;
	private int _index;

	public override void _Ready()
	{
		Debug.Assert(
			condition: IsInstanceValid(_dialogueGraphRef),
			message: "DialoguePlayback: invalid dialogue graph selected."
		);

		_count = _dialogueGraphRef.Nodes.Length;

		Debug.Assert(
			condition: _count > 0,
			message: "DialoguePlayback: selected dialogue graph is empty."
		);

		_scriptVariables = new();
		Reset();
	}

	#region Main Functions

	/// <summary>
	/// Sets/Updates a variable for the playback instance
	/// </summary>
	/// <param name="name">The name of the variable to set</param>
	/// <param name="value">Its value</param>
	public void SetVariable(StringName name, Variant value)
	{
		_scriptVariables[name] = value;

		// Apply changes to visible dialogue interfaces
		if (_currentBlock == null)
			return;

		SetCurrentBlock(_currentBlock);
	}

	/// <summary>
	/// Obtains the value of a specified variable from the playback instance.
	/// </summary>
	/// <param name="name">The name of the variable to get.</param>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <returns></returns>
	public T GetVariable<[MustBeVariant] T>(StringName name)
	{
		if (!_scriptVariables.ContainsKey(name))
			return default;

		return _scriptVariables[name].As<T>();
	}

	/// <summary>
	/// Jumps to a specific block in the script
	/// </summary>
	/// <param name="index">The index to jump to.</param>
	public void Jump(int index)
	{
		Debug.Assert(
			condition: index >= 0 && index < _count,
			message: "DialoguePlayback.Jump: Invalid jump index."
		);

		SetCurrentBlock(_dialogueGraphRef.Nodes[index]);
		_index = index;
	}

	/// <summary>
	/// Jumps to a specific block in the script
	/// </summary>
	/// <param name="tag">The unique tag to jump to.</param>
	public void Jump(StringName tag)
	{
		int? blockIdx = _dialogueGraphRef.FindIndex(tag);

		if (blockIdx == null)
			return;

		SetCurrentBlock(_dialogueGraphRef.Nodes[blockIdx.Value]);
		_index = blockIdx.Value + 1;
	}

	/// <summary>
	/// Jumps to a specific block in the script
	/// </summary>
	/// <param name="choice">The selected choice node</param>
	public void Jump(DialogueChoice choice)
	{
		Jump(choice.TargetTag);
	}

	/// <summary>
	/// Advances the script until a choice needs to made.
	/// </summary>
	/// <param name="wrap">Should it restart when it finishes?</param>
	public void Next(bool wrap = false)
	{
		if (HandleCommands())
			return;

		// Prevent advancing the dialogue if the player is expected to make a choice
		if (_currentBlock?.Choices?.Length > 0)
			return;

		if (_index >= _count) {
			if (wrap) {
				_index = 0;
			}
			else {
				EmitSignal(SignalName.PlaybackCompleted);
				return;
			}
		}

		SetCurrentBlock(_dialogueGraphRef.Nodes[_index]);
		_index ++;
	}

	/// <summary>
	/// Stops the playback and resets its index to zero.
	/// </summary>
	public void Stop()
	{
		EmitSignal(SignalName.PlaybackCompleted);
		Reset();
	}

	/// <summary>
	/// Resets the state of the playback.
	/// </summary>
	/// <param name="resetVariables">Whether or not to clear its variables.</param>
	public void Reset(bool resetVariables = true)
	{
		_currentBlock = null;
		_index = 0;

		if (!resetVariables)
			return;

		_scriptVariables.Clear();
	}

	#endregion

	#region Helpers

	private void SetCurrentBlock(DialogueNode block)
	{
		_currentBlock = block;

		string text = _currentBlock.DialogueText;

		text = DialogueParser.ParseAndResolveVariables(text, (StringName variableName) => {
			if (!_scriptVariables.ContainsKey(variableName)) {
				return variableName;
			}

			return _scriptVariables[variableName].ToString();
		});

		_nextCommands = _currentBlock.CommandList;

		// Automatically execute commands if a choice block is coming up
		if (_currentBlock.Choices?.Length > 0 && HandleCommands())
			return;

		EmitSignal(SignalName.CharacterUpdated, _currentBlock.CharacterId);
		EmitSignal(SignalName.DialogueTextUpdated, text);
		EmitSignal(SignalName.ChoicesUpdated, _currentBlock.Choices);
	}

	private bool HandleCommands()
	{
		if (_nextCommands == null) {
			return false;
		}

		ReadOnlySpan<DialogueNodeCommand> commands = _nextCommands;

		for (int i = 0; i < commands.Length; ++ i) {
			if (commands[i].Name == QuitCommand) {
				Stop();
				return true;
			}

			EmitSignal(SignalName.HandleCommandRequest, commands[i]);
		}

		return false;
	}

	#endregion
}
