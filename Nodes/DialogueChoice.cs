using Godot;

namespace SadChromaLib.Specialisations.Dialogue.Nodes;

/// <summary>
/// An object that represents a choice in a dialogue block.
/// </summary>
[GlobalClass]
public sealed partial class DialogueChoice: Resource
{
	[Export]
	public string ChoiceText;

	[Export]
	public string TargetTag;
}