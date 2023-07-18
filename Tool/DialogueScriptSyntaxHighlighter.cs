using Godot;
using Godot.Collections;

using System;

namespace SadChromaLib.Dialogue.Editor;

/// <summary>
/// A syntax highlighter for Dialogue Script files.
/// Attach it to a CodeEdit to use.
/// </summary>
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
			[DialogueParser.Type.Comment] = MakeColourDict(Colors.GreenYellow),
			[DialogueParser.Type.CharacterId] = MakeColourDict(Colors.SkyBlue),
			[DialogueParser.Type.Tag] = MakeColourDict(Colors.Orange),
			[DialogueParser.Type.Choice] = MakeColourDict(Colors.Cornsilk),
			[DialogueParser.Type.Command] = MakeColourDict(Colors.Pink),
			[DialogueParser.Type.DialogueLine] = MakeColourDict(_foregroundColour)
		};

		_variableColourDict = MakeColourDict(Colors.SeaGreen);
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
			bool isEndOfLine = i == line.Length - 1;

			if (start != null &&
				(DialogueParser.IsVariableTerminator(line[i]) || isEndOfLine))
			{
				result[start.Value] = _variableColourDict;

				if (!isEndOfLine) {
					result[i] = _foregroundColour;
				}

				return;
			}

			if (start == null &&
				line[i] == '$')
			{
				start = i;
			}
		}
	}

	private static Dictionary<StringName, Color> MakeColourDict(Color colour)
	{
		return new() {
			[KeyColour] = colour
		};
	}
}