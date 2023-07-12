using Godot;
using Godot.Collections;

using System;

namespace SadChromaLib.Dialogue;

[GlobalClass]
public sealed partial class DialogueScriptSyntaxHighlighter : SyntaxHighlighter
{
	[Export]
	private Color _foregroundColour = Colors.White;

	private const string KeyColour = "color";

	private Dictionary<DialogueParser.Type, Dictionary<StringName, Color>> _colourDicts;
	private Dictionary<StringName, Color> _variableColourDict;
	private Dictionary<StringName, Color> _targetTagColourDict;

	public DialogueScriptSyntaxHighlighter()
	{
		_colourDicts = new() {
			[DialogueParser.Type.CharacterId] = MakeColourDict(Colors.SkyBlue),
			[DialogueParser.Type.Tag] = MakeColourDict(Colors.Orange),
			[DialogueParser.Type.Choice] = MakeColourDict(Colors.Cornsilk),
			[DialogueParser.Type.Command] = MakeColourDict(Colors.Pink),
			[DialogueParser.Type.DialogueLine] = MakeColourDict(_foregroundColour)
		};

		_variableColourDict = MakeColourDict(Colors.SteelBlue);
		_targetTagColourDict = MakeColourDict(Colors.Salmon);
	}

	public override Dictionary _GetLineSyntaxHighlighting(int line)
	{
		Dictionary properties = new();

		ReadOnlySpan<char> textLine = GetTextEdit().GetLine(line);
		DialogueParser.Type type = DialogueParser.GetLineType(textLine);

		switch (type) {
			case DialogueParser.Type.Choice:
				DialogueParser.StripTabs(ref textLine);

				// Apply colouring depending on its inner type
				switch (DialogueParser.GetLineType(textLine)) {
					case DialogueParser.Type.DialogueLine:
						properties[0] = _colourDicts[DialogueParser.Type.Choice];
						break;

					case DialogueParser.Type.Tag:
						properties[0] = _targetTagColourDict;
						break;
				}
				break;

			case DialogueParser.Type.DialogueLine:
				ScanVariables(textLine, ref properties);
				break;

			default:
				properties[0] = _colourDicts[type];
				break;
		}

		return properties;
	}

	private void ScanVariables(ReadOnlySpan<char> line, ref Dictionary result)
	{
		int? start = null;

		for (int i = 0; i < line.Length; ++ i) {
			if (start != null && IsVariableTerminator(line[i])) {
				result[start.Value] = _variableColourDict;
				result[i] = _foregroundColour;
				return;
			}

			if (start == null &&
				line[i] == '$')
			{
				start = i;
			}
		}
	}

	private static bool IsVariableTerminator(char c)
	{
		return char.IsWhiteSpace(c) || !char.IsLetterOrDigit(c);
	}

	private static Dictionary<StringName, Color> MakeColourDict(Color colour)
	{
		return new() {
			[KeyColour] = colour
		};
	}
}