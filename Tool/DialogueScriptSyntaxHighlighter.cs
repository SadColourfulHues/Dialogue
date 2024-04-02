using Godot;
using Godot.Collections;

using System;
using System.Text.RegularExpressions;

namespace SadChromaLib.Specialisations.Dialogue.Editor;

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

	private Dictionary<DialogueParser.LineType, Dictionary<string, Color>> _colourDicts;
	private Dictionary<string, Color> _variableColourDict;
	private Dictionary<string, Color> _targetTagColourDict;

	public DialogueScriptSyntaxHighlighter()
	{
		_colourDicts = new() {
			[DialogueParser.LineType.Comment] = MakeColourDict(Colors.GreenYellow),
			[DialogueParser.LineType.Character] = MakeColourDict(Colors.Gray),
			[DialogueParser.LineType.Tag] = MakeColourDict(Colors.Orange),
			[DialogueParser.LineType.Choice] = MakeColourDict(Colors.Wheat),
			[DialogueParser.LineType.Command] = MakeColourDict(Colors.Pink),
			[DialogueParser.LineType.Dialogue] = MakeColourDict(_foregroundColour)
		};

		_variableColourDict = MakeColourDict(Colors.SeaGreen);
		_targetTagColourDict = MakeColourDict(Colors.Salmon);
	}

	public override Dictionary _GetLineSyntaxHighlighting(int line)
	{
		Dictionary properties = new();

		string textLine = GetTextEdit().GetLine(line);
		ReadOnlySpan<char> textLineSpan = textLine.AsSpan();
		DialogueParser.LineType type = DialogueParser.Identify(textLine);

		switch (type) {
			case DialogueParser.LineType.Choice:
				textLineSpan = DialogueParser.StripEmpty(textLineSpan);

				// Apply colouring depending on its inner type
				switch (DialogueParser.Identify(textLineSpan)) {
					case DialogueParser.LineType.Dialogue:
						properties[0] = _colourDicts[DialogueParser.LineType.Choice];
						break;

					case DialogueParser.LineType.Tag:
						properties[0] = _targetTagColourDict;
						break;
				}
				break;

			case DialogueParser.LineType.Dialogue:
				ScanVariables(textLine, ref properties);
				break;

			case DialogueParser.LineType.Comment:
			case DialogueParser.LineType.Character:
			case DialogueParser.LineType.Command:
			case DialogueParser.LineType.Tag:
				properties[0] = _colourDicts[type];
				break;

			default:
				return base._GetLineSyntaxHighlighting(line);
		}

		return properties;
	}

	private void ScanVariables(string line, ref Dictionary result)
	{
		MatchCollection matches = DialogueParser.RegexVars.Matches(line);

		for (int i = 0; i < matches.Count; ++i) {
			int start = matches[i].Index;

			result[start] = _variableColourDict;
			result[start + matches[i].Length] = _foregroundColour;
		}
	}

	private static Dictionary<string, Color> MakeColourDict(Color colour)
	{
		return new() {
			[KeyColour] = colour
		};
	}
}