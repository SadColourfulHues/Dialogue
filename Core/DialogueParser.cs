using Godot;

using System;
using System.Text;

using SadChromaLib.Specialisations.Dialogue.Nodes;

namespace SadChromaLib.Specialisations.Dialogue;

/// <summary>
/// A utility object that compiles dialogue scripts into DialogueGraph resource files.
/// </summary>
public sealed partial class DialogueParser: RefCounted
{
	public const string TagStart = "start";
	private const string ScriptTerminator = "\nEOF:";

	private const int MaxDialogueNodeCount = 512;
	private readonly StringBuilder _dialogueLineBuilder;

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