using Godot;

using System;
using System.Diagnostics;

namespace SadChromaLib.Specialisations.Dialogue.Nodes;

/// <summary>
/// An object containing dialogue blocks from a compiled Dialogue Script.
/// </summary>
[GlobalClass]
public sealed partial class DialogueGraph : Resource
{
	[Export]
	public DialogueNode[] Nodes;

	#region Nodes

	/// <summary>
	/// Finds a graph node with the specified tag.
	/// </summary>
	/// <param name="tag">The node block's unique tag.</param>
	/// <returns></returns>
	public DialogueNode FindNode(string tag)
	{
		int? index = FindIndex(tag);

		if (index == null)
			return null;

		return Nodes[index.Value];
	}

	/// <summary>
	/// Finds a graph node with the tag specified on a choice node.
	/// </summary>
	/// <param name="node">The choice node to use.</param>
	/// <returns></returns>
	public DialogueNode FindNode(DialogueChoice node)
	{
		Debug.Assert(
			condition: IsInstanceValid(node),
			message: "Graph.FindNode (Choice): node must be a valid choice node."
		);

		return FindNode(node.TargetTag);
	}

	/// <summary>
	/// Returns the index of a graph node with a specified tag
	/// </summary>
	/// <param name="tag">The node block's unique tag</param>
	/// <returns></returns>
	public int? FindIndex(string tag)
	{
		ReadOnlySpan<DialogueNode> nodes = Nodes;

		for (int i = 0; i < nodes.Length; ++ i) {
			if (nodes[i].Tag != tag)
				continue;

			return i;
		}

		return null;
	}

	/// <summary>
	/// Returns the first dialogue block.
	/// </summary>
	/// <returns></returns>
	public DialogueNode FirstNode()
	{
		Debug.Assert(
			condition: Nodes.Length > 0,
			message: "Graph.FirstNode: Graph is empty."
		);

		return Nodes[0];
	}

	/// <summary>
	/// Returns the last dialogue block.
	/// </summary>
	/// <returns></returns>
	public DialogueNode LastNode()
	{
		Debug.Assert(
			condition: Nodes.Length > 0,
			message: "Graph.LastNode: Graph is empty."
		);

		return Nodes[^1];
	}

	#endregion
}
