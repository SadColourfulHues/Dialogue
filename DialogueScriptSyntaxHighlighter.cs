using Godot;
using Godot.Collections;

namespace SadChromaLib.Dialogue;

[GlobalClass]
public sealed partial class DialogueScriptSyntaxHighlighter : SyntaxHighlighter
{
	private const string KeyColour = "color";

	public override Dictionary _GetLineSyntaxHighlighting(int line)
	{
		Color? colour = null;
		string textLine = GetTextEdit().GetLine(line);

		switch (DialogueParser.GetLineType(textLine))
		{
			case DialogueParser.Type.CharacterId:
				colour = Colors.SkyBlue;
				break;

			case DialogueParser.Type.Tag:
				colour = Colors.Orange;
				break;

			case DialogueParser.Type.Choice:
				colour = Colors.Wheat;
				break;

			case DialogueParser.Type.Command:
				colour = Colors.Pink;
				break;
		}

		if (colour == null) {
			return null;
		}

		Dictionary colourProps = new() {
			[KeyColour] = colour.Value
		};

		return new() {
			[0] = colourProps
		};
	}
}