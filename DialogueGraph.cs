using Godot;

namespace SadChromaLib.Dialogue;

/// <summary>
/// A structure that contains compiled information from a dialogue script.
/// </summary>
[GlobalClass]
public partial class DialogueGraph : Resource
{
	[Export]
	public DialogueNode[] Nodes;
}
