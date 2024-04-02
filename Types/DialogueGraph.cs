using System;
using System.Runtime.CompilerServices;

namespace SadChromaLib.Specialisations.Dialogue.Types;

/// <summary>
/// An object containing dialogue blocks from a compiled Dialogue Script.
/// </summary>
public sealed partial class DialogueGraph
{
	public DialogueNode[] Nodes;

	public DialogueGraph() {
		Nodes = null;
	}

	public DialogueGraph(DialogueNode[] nodes) {
		Nodes = nodes;
	}

	#region Nodes

	/// <summary>
	/// Finds a graph node with the specified tag.
	/// </summary>
	/// <param name="tag">The node block's unique tag.</param>
	/// <returns></returns>
	public DialogueNode? FindNode(string tag)
	{
		int? index = FindIndex(tag);

		if (index is null)
			return null;

		return Nodes[index.Value];
	}

	/// <summary>
	/// Finds a graph node with the tag specified on a choice node.
	/// </summary>
	/// <param name="node">The choice node to use.</param>
	/// <returns></returns>
	public DialogueNode? FindNode(DialogueChoice node) {
		return FindNode(node.TargetTag);
	}

	/// <summary>
	/// Returns the index of a graph node with a specified tag
	/// </summary>
	/// <param name="tag">The node block's unique tag</param>
	/// <returns></returns>
	public int? FindIndex(string tag)
	{
		ReadOnlySpan<DialogueNode> nodes = Nodes.AsSpan();

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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DialogueNode FirstNode()
	{
		return Nodes[0];
	}

	/// <summary>
	/// Returns the last dialogue block.
	/// </summary>
	/// <returns></returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public DialogueNode LastNode()
	{
		return Nodes[^1];
	}

	#endregion
}
