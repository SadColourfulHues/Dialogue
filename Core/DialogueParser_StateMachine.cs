using System;

using SadChromaLib.Specialisations.Dialogue.Nodes;

namespace SadChromaLib.Specialisations.Dialogue;

public sealed partial class DialogueParser
{
	private const int MaxTagLength = 16;

	private int _nodeIdx;
	private int _commandIdx;
	private int _choiceIdx;
	private string _lastCharacterName;
	private string _lastId;
	private string _lastTagId;
	private string _lastChoiceTagTarget;

	private State _state;
	private DialogueNode _lastNodeRef;
	private uint _id;

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
		ReadOnlySpan<char> idStr = _id.ToString();
		Span<char> tagId = stackalloc char[MaxTagLength];
		tagId[0] = 'n';
		tagId[1] = 'o';
		tagId[2] = 'd';
		tagId[3] = 'e';
		tagId[4] = '_';
		int tagIdx = 5;

		for (int i = 0; i < idStr.Length; ++ i) {
			tagId[tagIdx] = idStr[i];
			tagIdx ++;
		}

		_lastTagId = tagId[..tagIdx].ToString();
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
}