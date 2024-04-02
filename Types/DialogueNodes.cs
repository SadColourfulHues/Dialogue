using System;
using System.Diagnostics.CodeAnalysis;

namespace SadChromaLib.Specialisations.Dialogue.Types;

/// <summary>
/// A struct that represents a block of dialogue.
/// </summary>
public partial struct DialogueNode: IEquatable<DialogueNode>
{
	public const int MaxCommands = 16;
	public const int MaxChoices = 10;
	public const int MaxNodes = 2048;

	public string Tag;
	public string CharacterId;
	public string DialogueText;

	public DialogueCommand[] CommandList;
	public DialogueChoice[] Choices;

    #region Comparisons

	public bool Equals(DialogueNode other)
		=> Tag == other.Tag;

    public override bool Equals([NotNullWhen(true)] object obj)
    {
        if (obj is not DialogueNode node)
			return false;

		return node.Tag == Tag;
    }

    public override int GetHashCode()
		=> Tag.GetHashCode();

    public static bool operator ==(DialogueNode a, DialogueNode b) => a.Equals(b);
	public static bool operator !=(DialogueNode a, DialogueNode b) => !a.Equals(b);

	#endregion
}

/// <summary>
/// A struct that represents a choice in a dialogue block.
/// </summary>
public partial struct DialogueChoice
{
	public string ChoiceText;
	public string TargetTag;
}

/// <summary>
/// A struct that represents a command term in a dialogue block.
/// </summary>
public partial struct DialogueCommand
{
	public string Name;
	public string[] Parameters;
}