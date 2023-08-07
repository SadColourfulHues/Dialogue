using System;

using SadChromaLib.Specialisations.Dialogue.Nodes;

namespace SadChromaLib.Specialisations.Dialogue;

public sealed partial class DialogueParser
{
	private const int MaxCommands = 5;
	private const int MaxChoices = 8;

	private readonly (string Command, string Parameters)?[] _lastCommands;
	private readonly (string ChoiceText, string TargetTag)?[] _lastChoices;

	private void CreateAndAppendNode(ref Span<DialogueNode> nodes)
	{
		string dialogueText = _dialogueLineBuilder.ToString();
		_dialogueLineBuilder.Clear();

		_lastNodeRef = new() {
			Tag = _lastTagId,
			CharacterId = _lastCharacterName,
			DialogueText = dialogueText,
			CommandList = null,
			Choices = null
		};

		AppendNode(_lastNodeRef, ref nodes);
	}

	private void AppendNode(DialogueNode node, ref Span<DialogueNode> array)
	{
		if (_nodeIdx >= MaxDialogueNodeCount)
			return;

		array[_nodeIdx] = node;
		_nodeIdx ++;
	}

	private DialogueNode[] GetDialogueNodes(ref Span<DialogueNode> node)
	{
		return node[.._nodeIdx].ToArray();
	}

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
		_state = State.Idle;

		_dialogueLineBuilder.Clear();

		_lastTagId = TagStart;

		_lastId = null;
		_lastCharacterName = null;
		_lastChoiceTagTarget = null;
		_lastNodeRef = null;

		_commandIdx = 0;
		_choiceIdx = 0;
		_nodeIdx = 0;

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

	/// <summary>
	/// Returns a classification type for a specified line
	/// </summary>
	/// <param name="line">The line to classify</param>
	/// <returns></returns>
	public static Type GetLineType(ReadOnlySpan<char> line)
	{
		if (line.Length > 0 && line[0] == '#') {
			return Type.Comment;
		}
		else if (StartsWithTab(line)) {
			StripTabs(ref line);
			Type innerType = GetLineType(line);

			if (innerType == Type.Comment)
				return innerType;

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

	/// <summary>
	/// Returns whether or not a string starts with a tab or tab-like character(s)
	/// </summary>
	/// <param name="str">The string to check</param>
	/// <param name="minWhiteSpace">The minimum required amount of spaces that classifies as a 'tab'</param>
	/// <returns></returns>
	public static bool StartsWithTab(ReadOnlySpan<char> str, int minWhiteSpace = 4)
	{
		if (str.Length < 1)
			return false;

		if (str[0] == '\t')
			return true;

		if (str.Length < minWhiteSpace)
			return false;

		for (int i = 0; i < str.Length; ++ i) {
			if (char.IsWhiteSpace(str[i]))
				continue;

			return i >= minWhiteSpace;
		}

		return true;
	}

	/// <summary>
	/// Removes all tabs or tab-like characters from a string
	/// </summary>
	/// <param name="str">The string to strip</param>
	public static void StripTabs(ref ReadOnlySpan<char> str)
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

	/// <summary>
	/// Removes all whitespace, newline, and tab characters from a string
	/// </summary>
	/// <param name="line">The string to strip</param>
	public static void StripSpace(ref ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (char.IsWhiteSpace(line[i]) ||
				line[i] == '\t' ||
				line[i] == '\n')
			{
				continue;
			}

			line = line[..i];
		}
	}

	/// <summary>
	/// Returns whether or not a string is empty
	/// </summary>
	/// <param name="line">The string to check</param>
	/// <returns></returns>
	public static bool IsEmpty(ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] == '\t' || char.IsWhiteSpace(line[i]))
				continue;

			return false;
		}

		return true;
	}

	/// <summary>
	/// Returns whether or not the current character marks the end of a variable term
	/// </summary>
	/// <param name="c">The character to check</param>
	/// <returns></returns>
	public static bool IsVariableTerminator(char c)
	{
		return char.IsWhiteSpace(c) || !char.IsLetterOrDigit(c);
	}
}