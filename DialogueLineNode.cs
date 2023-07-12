using Godot;

namespace SadChromaLib.Dialogue;

[GlobalClass]
public partial class DialogueLineNode : DialogueNode
{
	[Export]
	public StringName CharacterId;

	[Export]
	string DialogueText;
}
