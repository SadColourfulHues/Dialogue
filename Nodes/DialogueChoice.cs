using Godot;

namespace SadChromaLib.Dialogue.Nodes;

[GlobalClass]
public sealed partial class DialogueChoice: Resource
{
	[Export]
	public string ChoiceText;

	[Export]
	public string TargetTag;
}