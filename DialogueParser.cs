using Godot;

using System;
using System.Text;

namespace SadChromaLib.Dialogue;

public sealed partial class DialogueParser: RefCounted
{
	private const string TagStart = "start";

	private int MaxCommands = 3;
	private int MaxChoices = 4;

	private State _state;
	private readonly StringBuilder _dialogueLineBuilder;

	private readonly (string Command, string Parameters)?[] _lastCommands;
	private readonly (string ChoiceText, string TargetTag)?[] _lastChoices;

	private int _commandIdx;
	private int _choiceIdx;
	private string _lastCharacterName;
	private string _lastId;
	private string _lastTagId;
	private string _lastChoiceTagTarget;

	private uint _id;

	public DialogueParser()
	{
		_lastCommands = new (string, string)?[MaxCommands];
		_lastChoices = new(string, string)?[MaxChoices];
		_dialogueLineBuilder = new();

		ResetState();
	}

	#region Main Functions

	/// <summary>
	/// <para>
	/// Parses a dialogue string with the following syntax.
	///	</para>
	///
	/// <para>
	/// @command_to_execute_after_dialogue
	/// </para>
	/// <para>
	/// [Tag]
	/// </para>
	/// <para>
	/// Character Name:
	/// </para>
	/// <para>
	/// Dialogue line 1.
	/// </para>
	/// <para>
	/// Dialogue line 2.
	/// </para>
	/// <para>
	/// etc...
	/// </para>
	/// <para>
	/// </para>
	/// <para>
	/// 	Choice A
	/// </para>
	/// <para>
	/// 	[tag to go to]
	/// </para>
	/// <para>
	/// 	Choice B
	/// </para>
	/// <para>
	/// 	[choice_b]
	/// </para>
	///
	/// <para>
	/// [choice_b]
	/// </para>
	/// <para>
	/// Character Name:
	/// </para>
	/// <para>
	/// So you chose B, that's good.
	/// </para>
	/// </summary>
	/// <param name="dialogue"></param>
	public void Parse(string dialogue)
	{
		ResetState();

		Span<string> lines = dialogue
			.Split("\n");

		for (int i = 0; i < lines.Length; ++ i) {
			ReadOnlySpan<char> line = lines[i];

			if (IsEmpty(ref line))
				continue;

			Type type = GetLineType(line);
			StripTabs(ref line);

			ParseLine(line, type);
		}
	}

	private void ParseLine(ReadOnlySpan<char> line, Type type)
	{
		switch (_state) {
			case State.Idle:
				ProcessIdle(line, type);
				break;

			case State.Dialogue:
				ProcessDialogueLine(line, type);
				break;

			case State.Choice:
				ProcessChoice(line, type);
				break;
		}
	}

	#endregion

	#region State Machine

	private void ProcessIdle(ReadOnlySpan<char> line, Type type)
	{
		switch (type) {
			case Type.CharacterId:
				_lastCharacterName = ParseCharacterId(line);
				_state = State.Dialogue;
				break;

			case Type.DialogueLine:
				_dialogueLineBuilder.AppendLine(line.ToString());
				break;

			case Type.Command:
				CommandInfo command = ParseCommand(line);
				AppendCommand(command);
				break;

			case Type.Tag:
				_lastTagId = ParseTagId(line);
				break;

			case Type.Choice:
				_state = State.Choice;
				ParseLine(line, type);
				break;
		}
	}

	private void ProcessDialogueLine(ReadOnlySpan<char> line, Type type)
	{
		if (type == Type.DialogueLine) {
			AppendDialogueLine(line);
			return;
		}

		string dialogueText = _dialogueLineBuilder.ToString();
		_dialogueLineBuilder.Clear();

		// <debug>
		GD.Print($"[{_lastTagId}] {_lastCharacterName} says\n{dialogueText}");

		for (int i = 0; i < MaxCommands; ++ i) {
			if (_lastCommands[i] == null)
				continue;

			(string command, string parameters) = _lastCommands[i].Value;
			GD.Print($"{command}: {parameters}");
		}

		//</debug>

		ClearCommands();

		// Assign unique ID for untagged nodes
		_lastTagId = $"node_{_id}";
		_id ++;

		// A dialogue node has been extracted,
		// continue parsing the current line back in its regular state

		_state = State.Idle;
		ParseLine(line, type);
	}

	private void ProcessChoice(ReadOnlySpan<char> line, Type type)
	{
		Type innerType = GetLineType(line);

		if (innerType == Type.DialogueLine) {
			AppendDialogueLine(line);
			return;
		}

		if (innerType == Type.Tag) {
			_lastChoiceTagTarget = ParseTagId(line);
		}

		if (type != Type.Choice || innerType == Type.Tag) {
			string choiceText = _dialogueLineBuilder.ToString();

			if (choiceText.Length > 0) {
				_dialogueLineBuilder.Clear();
				AppendChoice(choiceText, _lastChoiceTagTarget);
			}

			if (type == Type.Choice)
				return;

			// <debug>

			for (int i = 0; i < MaxChoices; ++ i) {
				if (_lastChoices[i] == null)
					continue;

				(string text, string targetId) = _lastChoices[i].Value;
				GD.Print($"Choice ({targetId}): \"{text}\"");
			}

			// </debug>

			ClearChoices();

			_state = State.Idle;
			ParseLine(line, type);
		}
	}

	#endregion

	#region Parsers

	private static CommandInfo ParseCommand(ReadOnlySpan<char> line)
	{
		ReadOnlySpan<char> parameters = line;

		if (!line.Contains(' ')) {
			return new() {
				Name = line[1..],
				Parameter = null
			};
		}

		for (int i = 1; i < line.Length; ++ i) {
			if (!char.IsWhiteSpace(line[i]))
				continue;

			parameters = line[(i + 1)..];
			line = line[1..i];
			break;
		}

		return new() {
			Name = line,
			Parameter = parameters
		};
	}

	private static string ParseCharacterId(ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] != ':')
				continue;

			line = line[..i];
		}

		return line.ToString();
	}

	private static string ParseTagId(ReadOnlySpan<char> line)
	{
		if (line.Length < 3)
			return null;

		int start = 0;

		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] == '[') {
				start = i + 1;
			}
			else if (line[i] == ']') {
				line = line[start..i];
				break;
			}
		}

		return line.ToString();
	}

	#endregion

	#region Helpers

	private void AppendDialogueLine(ReadOnlySpan<char> line)
	{
		if (_dialogueLineBuilder.Length < 1) {
			_dialogueLineBuilder.Append(line);
			return;
		}

		_dialogueLineBuilder.AppendLine(line.ToString());
	}

	private void AppendCommand(CommandInfo command)
	{
		AppendToTemporaryDataArray(
			array: _lastCommands,
			paramA: command.Name.ToString(),
			paramB: command.Parameter.ToString(),
			max: MaxCommands,
			index: ref _commandIdx
		);
	}

	private void AppendChoice(string choiceText, string targetTag)
	{
		AppendToTemporaryDataArray(
			array: _lastChoices,
			paramA: choiceText,
			paramB: targetTag,
			max: MaxChoices,
			index: ref _choiceIdx
		);
	}

	private void ClearCommands()
	{
		ClearTemporaryDataArray(_lastCommands, MaxCommands, ref _commandIdx);
	}

	private void ClearChoices()
	{
		ClearTemporaryDataArray(_lastChoices, MaxChoices, ref _choiceIdx);
	}

	private void ResetState()
	{
		_id = 0;
		_lastCharacterName = null;
		_state = State.Idle;

		_lastId = null;
		_lastTagId = TagStart;
		_lastChoiceTagTarget = null;

		_dialogueLineBuilder.Clear();

		ClearCommands();
		ClearChoices();
	}

	private static void ClearTemporaryDataArray(
		(string, string)?[] array,
		int max,
		ref int index)
	{
		for (int i = 0; i < max; ++ i) {
			array[i] = null;
		}

		index = 0;
	}

	private static void AppendToTemporaryDataArray(
		(string, string)?[] array,
		string paramA,
		string paramB,
		int max,
		ref int index)
	{
		if (index >= max)
			return;

		array[index] = new(paramA, paramB);
		index ++;
	}

	public static Type GetLineType(ReadOnlySpan<char> line)
	{
		if (StartsWithTab(line)) {
			return Type.Choice;
		}
		else if (line.Length > 1 && line[0] == '[' && line[^1] == ']') {
			return Type.Tag;
		}
		else if (line.Length > 0 && line[0] == '@') {
			return Type.Command;
		}
		else if (line.Length > 0 && line[^1] == ':') {
			return Type.CharacterId;
		}

		return Type.DialogueLine;
	}

	private static bool StartsWithTab(ReadOnlySpan<char> str, int minWhiteSpace = 4)
	{
		if (str.Length < 1)
			return false;

		if (str[0] == '\t')
			return true;

		for (int i = 0; i < str.Length; ++ i) {
			if (char.IsWhiteSpace(str[i]))
				continue;

			return i >= minWhiteSpace;
		}

		return true;
	}

	private static void StripTabs(ref ReadOnlySpan<char> str)
	{
		if (str.Length < 1)
			return;

		if (str[0] == '\t') {
			str = str[1..];
			return;
		}

		for (int i = 0; i < str.Length; ++ i) {
			if (char.IsWhiteSpace(str[i]))
				continue;

			str = str[i..];
			return;
		}
	}

	private static bool IsEmpty(ref ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] == '\t' || char.IsWhiteSpace(line[i]))
				continue;

			return false;
		}

		return true;
	}

	#endregion

	private ref struct CommandInfo
	{
		public ReadOnlySpan<char> Name;
		public ReadOnlySpan<char> Parameter;
	}

	/// <summary>
	/// Classification type for a specified text line
	/// </summary>
	public enum Type
	{
		CharacterId,
		DialogueLine,
		Command,
		Choice,
		Tag
	}

	/// <summary>
	/// An enum describing the current state of the parser
	/// </summary>
	private enum State
	{
		Idle,
		Dialogue,
		Choice
	}
}