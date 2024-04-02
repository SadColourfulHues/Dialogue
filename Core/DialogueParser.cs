using Godot;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

using SadChromaLib.Persistence;
using SadChromaLib.Specialisations.Dialogue.Types;
using SadChromaLib.Types;

using GFAccess = Godot.FileAccess;

namespace SadChromaLib.Specialisations.Dialogue;

/// <summary>
/// A utility object that parses/compiles dialogue scripts into dialogue graph files.
/// </summary>
public sealed class DialogueParser
{
	const int KNode = 0;
	const int KChoice = 1;

	public static Regex RegexVars = new("\\$([\\w]+)", RegexOptions.Compiled);
	public static Regex RegexCharacter = new("(\\w+):", RegexOptions.Compiled);
	public static Regex RegexTag = new("\\[(\\w+)\\]", RegexOptions.Compiled);
	public static Regex RegexCommand = new("@([\\w]+)( [\\w,\\s]+)?", RegexOptions.Compiled);

	readonly StringBuilder _dialogueLineBuilder;

	readonly bool[] _hasData;
	readonly XArray<DialogueNode> _tmpNodes;
	readonly XArray<DialogueChoice> _tmpChoices;
	readonly XArray<DialogueCommand> _tmpCommands;

	DialogueNode _tmpNode;
	DialogueChoice _tmpChoice;

	int? _nodeIdx;

	public DialogueParser()
	{
		_hasData = new bool[2];

		_tmpNode = default;
		_tmpChoice = default;

		_tmpNodes = new(DialogueNode.MaxNodes);
		_tmpChoices = new(DialogueNode.MaxChoices);
		_tmpCommands = new(DialogueNode.MaxCommands);

		_hasData[KNode] = false;
		_hasData[KChoice] = false;

		_dialogueLineBuilder = new();
	}

	#region Main Functions

	/// <summary>
	/// Parses a dialogue script string and encodes it into a graph
	/// </summary>
	/// <param name="dialogue"></param>
	/// <returns></returns>
	public DialogueGraph Compile(string dialogue)
	{
		_tmpNodes.Clear();
		_tmpChoices.Clear();
		_tmpCommands.Clear();

		_tmpNode = default;
		_tmpChoice = default;

		_dialogueLineBuilder.Clear();

		State state = State.Idle;
		_nodeIdx = null;

		_hasData[KNode] = false;
		_hasData[KChoice] = false;

		MemoryStream buffer = new(Encoding.UTF8.GetBytes(dialogue));

		using (StreamReader reader = new(buffer)) {
			while (!reader.EndOfStream) {
				string line = reader.ReadLine();

				if (line is null)
					break;

				LineType type = Identify(line.AsSpan());

				// Skip irrelevant lines
				if (type == LineType.Irrelevant || type == LineType.Comment)
					continue;

				ParseLine(line, type, ref state);
			}

			// Commit pending nodes before terminating
			CommitNode();
		}

		return new(_tmpNodes.ToArray());
	}

	/// <summary>
	/// Compiles a dialogue script and writes it into a serialised binary file
	/// </summary>
	public void CompileToFile(string dialogue, string filePath)
	{
		DialogueGraph graph = Compile(dialogue);
		FileStream file = File.Open(filePath, FileMode.OpenOrCreate);

		using (PersistenceWriter writer = new(file)) {
			writer.Write(graph);
		}
	}

	/// <summary>
	/// Reads a serialised binary file and creates a dialogue graph from it
	/// </summary>
	/// <returns></returns>
	public static bool Load(string filePath, out DialogueGraph graph)
	{
		graph = null;

		// Note to future self:

		// Brief version:
		// Working with packaged files can be tricky, so we'll just
		// defer to using Godot's file IO APIs when loading dialogue graphs.

		// Slightly-longer-version:
		// It's not so much a big of a deal when writing files,
		// since we typically do them during production -- when no files
		// are 'bundled' along our main application.
		// But reading is expected to happen at runtime, and when
		// exported to a proper build, C#'s file API will simply fail when
		// loading 'res://' files. (or what currently happens, atm [Godot 4.2])

		if (!GFAccess.FileExists(filePath)) {
			GD.PrintErr($"DialogueParser: \"{filePath}\" does not exist!");
			return false;
		}

		// Dump file data to a memory stream so we can still use C#'s binary IO APIs
		MemoryStream dataStream = new(GFAccess.GetFileAsBytes(filePath));

		using (PersistenceReader reader = new(dataStream)) {
			graph = new();
			reader.ReadToComponent(graph);
		}

		return true;
	}

	#endregion

	#region Parser Utils

	private bool CommitNode()
	{
		if (!_hasData[KNode])
			return false;

		CommitChoice();

		// Assign node tag //
		if (_nodeIdx is null) {
			_tmpNode.Tag = "start";
			_nodeIdx = 1;
		}
		else if (_tmpNode.Tag is null) {
			_tmpNode.Tag = "node_" + _nodeIdx;
			_nodeIdx ++;
		}

		// Finish writing the current node
		_tmpNode.DialogueText = _dialogueLineBuilder.ToString();
		_tmpNode.Choices = _tmpChoices.ToArray();
		_tmpNode.CommandList = _tmpCommands.ToArray();

		_tmpNodes.Add(_tmpNode);

		_tmpChoices.Clear();
		_tmpCommands.Clear();
		_dialogueLineBuilder.Clear();

		_hasData[KNode] = false;
		_tmpNode = default;

		return true;
	}

	private bool CommitChoice()
	{
		if (!_hasData[KChoice])
			return false;

		_tmpChoices.Add(_tmpChoice);

		_hasData[KChoice] = false;
		_tmpChoice = default;

		return true;
	}

	private void CommitCommand(string line)
	{
		Match match = RegexCommand.Match(line);

		if (match.Groups[2].Length > 0) {
			_tmpCommands.Add(new() {
				Name = match.Groups[1].Value,
				Parameters = match.Groups[2].Value.Split(' ')
			});
		}
		else {
			_tmpCommands.Add(new() {
				Name = match.Groups[1].Value,
				Parameters = null
			});
		}
	}

	private string ParseTag(string line)
	{
		Match tagMatch = RegexTag.Match(line);

		if (!tagMatch.Success) {
			GD.PrintErr(line, " (Error: malformed tag attribute.)");
			return line;
		}

		return tagMatch.Groups[1].Value;
	}

	#endregion


	#region Parser State Machine

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ChangeState(State state, string line, LineType type, ref State currentState)
	{
		currentState = state;
		ParseLine(line, type, ref currentState);
	}

	private void ParseLine(string line, LineType type, ref State state)
	{
		/*
			(Note to future self)
			This is the parser state machine's entry point,
			it shouldn't do anything other than redirect to the proper processing method.
		*/

		switch (state)
		{
			case State.Idle:
			case State.Dialogue:
				ParseDialogue(line, type, ref state);
				break;

			case State.Choice:
				ParseChoice(line, type, ref state);
				break;
		}
	}

	private void ParseDialogue(string line, LineType type, ref State state)
	{
		// (note: this will only trigger if the current block has at least one dialogue line!)

		// If we come into another character tag, assume that we've left
		// the scope of the previous dialogue block
		if ((type == LineType.Character) && CommitNode())
		{
			ChangeState(State.Idle, line, type, ref state);
			return;
		}

		/*
			* dialogue block structure *
			Character:														[character]
			[important_story_beat_i_promise]								[tag]
			Oh my gog, I'm actually chatting!								[dialogue]
			So honoured to do this, so here's another dialogue line!		[dialogue]

			@command_b														[command]

				* Choice Block *											[choice]
				Yay																[choice text]
				[slash_pos]														[choice target]

				WTF
				[slash_neg]													[/choice]

			@command_a														[command]
		*/

		switch (type)
		{
			case LineType.Tag:
				_tmpNode.Tag = ParseTag(line);
				break;

			case LineType.Character:
				Match characterMatch = RegexCharacter.Match(line);

				if (!characterMatch.Success) {
					GD.PrintErr(line, " (Error: malformed character block.)");
					break;
				}

				// (Note to future self)
				// If a dialogue block has no data, this statement will be ran
				// Instead of the state terminator at the start of this method
				_tmpNode.CharacterId = characterMatch.Groups[1].Value;
				break;

			case LineType.Dialogue:
				if (_dialogueLineBuilder.Length < 1) {
					_dialogueLineBuilder.Append(line);
				}
				else {
					_dialogueLineBuilder.AppendLine(line);
				}

				_hasData[KNode] = true;
				break;

			case LineType.Choice:
				ChangeState(State.Choice, line, type, ref state);
				break;

			case LineType.Command:
				CommitCommand(line);
				break;
		}
	}

	private void ParseChoice(string line, LineType type, ref State state)
	{
		// We've left the choice block
		if (type != LineType.Choice) {
			CommitChoice();
			ChangeState(State.Dialogue, line, type, ref state);
			return;
		}

		string innerLine = StripEmpty(line).ToString();
		LineType innerType = Identify(innerLine.AsSpan());

		/*
			* choice block structure *

			I choose pink!	(dialogue)
			[pink_chosen]	(tag)

			I choose red!	<- choice text
			[red_chosen]	<- target tag
		*/

		switch (innerType)
		{
			case LineType.Dialogue:
				if (CommitChoice()) {
					ParseChoice(line, type, ref state);
					return;
				}

				_tmpChoice.ChoiceText = innerLine;
				break;

			case LineType.Tag:
				_tmpChoice.TargetTag = ParseTag(line);
				_hasData[KChoice] = true;
				break;
		}
	}

	#endregion

	#region Utilities

	/// <summary>
	/// Finds variable instances in a text and passes their names into a resolver callback method
	/// </summary>
	/// <returns></returns>
	public static string ParseAndResolveVariables(string text, Func<string, string> resolveCallback)
		=> RegexVars.Replace(text, (Match m) => resolveCallback(m.Value[1..]));

	/// <summary>
	/// Returns whether or not a string starts with a tab or tab-like character(s)
	/// </summary>
	/// <param name="str">The string to check</param>
	/// <param name="minWhiteSpace">The minimum required amount of spaces that classifies as a 'tab'</param>
	/// <returns></returns>
	public static bool StartsWithTab(ReadOnlySpan<char> str, int minWhiteSpace = 4)
	{
		if (str.Length < 1 || str.Length < minWhiteSpace)
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

	/// <summary>
	/// Removes 'empty' characters in a string
	/// </summary>
	/// <returns></returns>
	public static ReadOnlySpan<char> StripEmpty(ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++i) {
			if (char.IsWhiteSpace(line[i]) || line[i] == '\t')
				continue;

			return line[i..];
		}

		return line;
	}

	/// <summary>
	/// Identifies the line type
	/// </summary>
	/// <param name="line">The line to analyse</param>
	/// <returns></returns>
	public static LineType Identify(ReadOnlySpan<char> line)
	{
		if (line.Length < 1)
			return LineType.Irrelevant;

		ReadOnlySpan<char> sline = StripEmpty(line);

		if (line[0] == '#' || (sline.Length > 0 && sline[0] == '#')) {
			return LineType.Comment;
		}
		else if (StartsWithTab(line)) {
			return LineType.Choice;
		}

		if (sline.Length < 1)
			return LineType.Irrelevant;

		if (sline[^1] == ':') {
			return LineType.Character;
		}
		else if (sline[0] == '[' && sline[^1] == ']') {
			return LineType.Tag;
		}
		else if (sline[0] == '@') {
			return LineType.Command;
		}

		return LineType.Dialogue;
	}

	#endregion

	enum State
	{
		Idle,
		Dialogue,
		Choice
	}

	public enum LineType
	{
		Irrelevant,
		Comment,
		Tag,
		Character,
		Dialogue,
		Choice,
		Command
	}
}