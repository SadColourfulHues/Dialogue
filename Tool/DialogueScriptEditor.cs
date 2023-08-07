using Godot;

namespace SadChromaLib.Specialisations.Dialogue.Editor;

public partial class DialogueScriptEditor : CodeEdit
{
	public override void _Ready()
	{
		SyntaxHighlighter = new DialogueScriptSyntaxHighlighter();
	}
}
