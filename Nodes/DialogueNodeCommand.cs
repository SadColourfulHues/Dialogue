using Godot;

namespace SadChromaLib.Dialogue.Nodes;

/// <summary>
/// The only reason this thing exists is because Godot still doesn't support exporting tuples and/or dictionaries
/// </summary>
[GlobalClass]
public sealed partial class DialogueNodeCommand: Resource
{
	[Export]
	public StringName Name;

	[Export]
	public string Parameter;
}