using Godot;

namespace SadChromaLib.Specialisations.Dialogue.Nodes;

/// <summary>
/// An object that represents a block of dialogue.
/// </summary>
[GlobalClass]
public sealed partial class DialogueNode: Resource
{
	[Export]
	public StringName Tag;

	[Export]
	public StringName CharacterId;

	[Export]
	public string DialogueText;

	[Export]
	public DialogueNodeCommand[] CommandList;

	[Export]
	public DialogueChoice[] Choices;
}