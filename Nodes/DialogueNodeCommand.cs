using Godot;

namespace SadChromaLib.Specialisations.Dialogue.Nodes;

/// <summary>
/// An object that represents a command term in a dialogue block.
/// </summary>
[GlobalClass]
public sealed partial class DialogueNodeCommand: Resource
{
	[Export]
	public StringName Name;

	[Export]
	public string Parameter;
}