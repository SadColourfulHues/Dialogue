using Godot;

using System;
using System.Text;

using SadChromaLib.Dialogue.Nodes;

namespace SadChromaLib.Dialogue;

/// <summary>
/// A utility object that compiles dialogue scripts into DialogueGraph resource files.
/// </summary>
public sealed partial class DialogueParser: RefCounted
{
	public const string TagStart = "start";
	private const string ScriptTerminator = "\nEOF:";

	private const int MaxDialogueNodeCount = 512;
	private const int MaxDialogueLineLength = 1024;
	private const int MaxParameterCount = 16;

	private const int MaxCommands = 5;
	private const int MaxChoices = 4;

	private readonly StringBuilder _dialogueLineBuilder;

	private readonly (string Command, string Parameters)?[] _lastCommands;
	private readonly (string ChoiceText, string TargetTag)?[] _lastChoices;

	private State _state;

	private int _nodeIdx;
	private int _commandIdx;
	private int _choiceIdx;
	private string _lastCharacterName;
	private string _lastId;
	private string _lastTagId;
	private string _lastChoiceTagTarget;

	private DialogueNode _lastNodeRef;
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
	/// Parses and compiles a dialogue string with the following syntax.
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
	public DialogueGraph Compile(string dialogue)
	{
		ResetState();

		Span<DialogueNode> nodes = new DialogueNode[MaxDialogueNodeCount];

		ReadOnlySpan<string> lines = (dialogue + ScriptTerminator)
			.Split("\n");

		for (int i = 0; i < lines.Length; ++ i) {
			ReadOnlySpan<char> line = lines[i];

			if (IsEmpty(line))
				continue;

			Type type = GetLineType(line);
			StripTabs(ref line);

			Process(line, type, ref nodes);
		}

		return new() {
			Nodes = GetDialogueNodes(ref nodes)
		};
	}

	#endregion

	#region State Machine

	private void Process(ReadOnlySpan<char> line, Type type, ref Span<DialogueNode> nodes)
	{
		switch (_state) {
			case State.Idle:
				ProcessIdle(line, type, ref nodes);
				break;

			case State.Dialogue:
				ProcessDialogueLine(line, type, ref nodes);
				break;

			case State.Command:
				ProcessCommand(line, type, ref nodes);
				break;

			case State.Choice:
				ProcessChoice(line, type, ref nodes);
				break;
		}
	}

	private void ProcessIdle(ReadOnlySpan<char> line, Type type, ref Span<DialogueNode> nodes)
	{
		switch (type) {
			case Type.Command:
				_state = State.Command;
				Process(line, type, ref nodes);
				break;

			case Type.Choice:
				_state = State.Choice;
				Process(line, type, ref nodes);
				break;

			case Type.DialogueLine:
				AppendDialogueLine(line);
				break;

			case Type.Tag:
				_lastTagId = ParseTagId(line);
				break;

			case Type.CharacterId:
				_state = State.Dialogue;
				_lastCharacterName = ParseCharacterId(line);
				break;
		}
	}

	private void ProcessDialogueLine(ReadOnlySpan<char> line, Type type, ref Span<DialogueNode> nodes)
	{
		if (type == Type.DialogueLine) {
			AppendDialogueLine(line);
			return;
		}

		CreateAndAppendNode(ref nodes);

		// Assign unique ID for untagged nodes
		_lastTagId = $"node_{_id}";
		_id ++;

		// A dialogue node has been extracted,
		// continue parsing the current line back in its regular state

		_state = State.Idle;
		Process(line, type, ref nodes);
	}

	private void ProcessCommand(ReadOnlySpan<char> line, Type type, ref Span<DialogueNode> nodes)
	{
		if (type == Type.Command) {
			CommandInfo commandInfo = ParseCommand(line);
			AppendCommand(commandInfo);
			return;
		}

		if (_lastNodeRef != null) {
			ReadOnlySpan<(string, string)?> commands = _lastCommands;
			Span<DialogueNodeCommand> commandList = new DialogueNodeCommand[MaxCommands];
			int commandIdx = 0;

			for (int i = 0; i < MaxCommands; ++ i) {
				if (commands[i] == null)
					continue;

				(string commandName, string parameter) = commands[i].Value;

				commandList[commandIdx] = new() {
					Name = commandName,
					Parameter = parameter
				};

				commandIdx ++;
			}

			_lastNodeRef.CommandList = commandList[..commandIdx].ToArray();
		}

		ClearCommands();

		_state = State.Idle;
		Process(line, type, ref nodes);
	}

	private void ProcessChoice(ReadOnlySpan<char> line, Type type, ref Span<DialogueNode> nodes)
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

			// Once we detect that we've left the scope of the choice section,
			// Combine the extracted data then continue parsing the current line using its default behaviour
			if (type == Type.Choice)
				return;

			if (_lastNodeRef != null) {
				int choiceIdx = 0;

				Span<DialogueChoice> choiceList = new DialogueChoice[MaxChoices];
				ReadOnlySpan<(string, string)?> choices = _lastChoices;

				for (int i = 0; i < MaxChoices; ++ i) {
					if (choices[i] == null)
						continue;

					(string text, string tag) = choices[i].Value;

					choiceList[choiceIdx] = new() {
						ChoiceText = text,
						TargetTag = tag
					};

					choiceIdx ++;
				}

				_lastNodeRef.Choices = choiceList[..choiceIdx].ToArray();
			}

			ClearChoices();

			_state = State.Idle;
			Process(line, type, ref nodes);
		}
	}

	#endregion

	#region Parsers

	/// <summary>
	/// Extracts command statement information from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static CommandInfo ParseCommand(ReadOnlySpan<char> line)
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

	/// <summary>
	/// Extracts the character's ID from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static string ParseCharacterId(ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] != ':')
				continue;

			line = line[..i];
		}

		return line.ToString();
	}

	/// <summary>
	/// Extracts a tag ID from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static string ParseTagId(ReadOnlySpan<char> line)
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

	/// <summary>
	/// Parses a string for variable terms and replaces each occurrence accordingly
	/// </summary>
	/// <param name="text">The string to parse</param>
	/// <param name="resolveCallback">A callback method that supplies variable values.</param>
	/// <returns></returns>
	public static string ParseAndResolveVariables(string text, Func<StringName, string> resolveCallback)
	{
		ReadOnlySpan<char> characters = text;

		// Naive variable usage test
		if (!characters.Contains('$'))
			return text;

		Span<char> tmpString = stackalloc char[MaxDialogueLineLength];
		int strIdx = 0;
		int? startIdx = null;
		int endLen = characters.Length - 1;

		for (int i = 0; i < characters.Length; ++ i) {
			bool isEndOfLine = i == endLen;

			// Look for variable end
			if (startIdx != null &&
				(IsVariableTerminator(characters[i]) || isEndOfLine))
			{
				ReadOnlySpan<char> variableName;
				int sliceLen;

				if (isEndOfLine) {
					sliceLen = endLen - startIdx.Value;
					variableName = characters[startIdx.Value..];
				}
				else {
					sliceLen = i - startIdx.Value;
					variableName = characters.Slice(startIdx.Value, sliceLen);
				}

				strIdx -= sliceLen;

				ReadOnlySpan<char> variableValue = resolveCallback.Invoke(variableName.ToString());

				// Overwrite variable name with value
				for (int j = 0; j < variableValue.Length; ++ j) {
					tmpString[strIdx] = variableValue[j];
					strIdx ++;
				}

				startIdx = null;
			}
			// Look for variable start
			else if (startIdx == null && characters[i] == '$') {
				startIdx = i + 1;
			}
			// Copy non-variable characters as-is
			else {
				tmpString[strIdx] = characters[i];
				strIdx ++;
			}
		}

		return tmpString[..strIdx].ToString();
	}

	/// <summary>
	/// Parses a multi-parameter command into a span of strings
	/// </summary>
	/// <param name="parameterStr">The parameter string to parse</param>
	/// <param name="parameters">A span of strings to store the parameters</param>
	public static void ParseCommandParameters(ReadOnlySpan<char> parameterStr, ref Span<string> parameters)
	{
		if (parameterStr.Length < 1)
			return;

		Span<string> tmpParameters = new string[MaxParameterCount];

		int? lastStart = 0;
		int parameterIdx = 0;

		int lastAdded = 0;

		for (int i = 0; i < parameterStr.Length; ++ i) {
			if (lastStart != null && char.IsWhiteSpace(parameterStr[i])) {
				ExtractCommandParameterStr(
					parameter: parameterStr,
					start: lastStart.Value,
					end: i,
					parameters: ref tmpParameters,
					parameterIdx: ref parameterIdx
				);

				lastStart = null;
				lastAdded = i + 1;
			}
			else if (lastStart == null && char.IsWhiteSpace(parameterStr[i])) {
				lastStart = i;
			}
		}

		ExtractCommandParameterStr(
			parameter: parameterStr,
			start: lastAdded,
			end: parameterStr.Length,
			parameters: ref tmpParameters,
			parameterIdx: ref parameterIdx
		);

		parameters = tmpParameters[..parameterIdx];
	}

	private static void ExtractCommandParameterStr(ReadOnlySpan<char> parameter, int start, int end, ref Span<string> parameters, ref int parameterIdx)
	{
		int sliceLen = end - start;
		ReadOnlySpan<char> slice = parameter.Slice(start, sliceLen);

		parameters[parameterIdx] = slice.ToString();
		parameterIdx ++;
	}

	#endregion

	#region Helpers

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

	#endregion

	/// <summary>
	/// A structure holding parsed command statement information
	/// </summary>
	public ref struct CommandInfo
	{
		public ReadOnlySpan<char> Name;
		public ReadOnlySpan<char> Parameter;
	}

	/// <summary>
	/// Classification type for a specified text line
	/// </summary>
	public enum Type
	{
		Comment,
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
		Command,
		Choice
	}
}