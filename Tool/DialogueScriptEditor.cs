using Godot;

using System;

namespace SadChromaLib.Dialogue.Editor;

public partial class DialogueScriptEditor : CodeEdit
{
	public override void _Ready()
	{
		SyntaxHighlighter = new DialogueScriptSyntaxHighlighter();
	}
}
