using Godot;
using System;

namespace SadChromaLib.Specialisations.Dialogue;

public sealed partial class DialogueParser
{
	private const int MaxDialogueLineLength = 1024;
	private const int MaxParameterCount = 16;

	/// <summary>
	/// Extracts command statement information from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static CommandInfo ParseCommand(ReadOnlySpan<char> line)
	{
		ReadOnlySpan<char> parameters = line;

		if (!line.Contains(' ')) {
			return new() {
				Name = line[1..],
				Parameter = null
			};
		}

		for (int i = 1; i < line.Length; ++ i) {
			if (!char.IsWhiteSpace(line[i]))
				continue;

			parameters = line[(i + 1)..];
			line = line[1..i];
			break;
		}

		return new() {
			Name = line,
			Parameter = parameters
		};
	}

	/// <summary>
	/// Extracts the character's ID from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static string ParseCharacterId(ReadOnlySpan<char> line)
	{
		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] != ':')
				continue;

			line = line[..i];
		}

		return line.ToString();
	}

	/// <summary>
	/// Extracts a tag ID from a line
	/// </summary>
	/// <param name="line">The line to parse</param>
	/// <returns></returns>
	public static string ParseTagId(ReadOnlySpan<char> line)
	{
		if (line.Length < 3)
			return null;

		int start = 0;

		for (int i = 0; i < line.Length; ++ i) {
			if (line[i] == '[') {
				start = i + 1;
			}
			else if (line[i] == ']') {
				line = line[start..i];
				break;
			}
		}

		return line.ToString();
	}

	/// <summary>
	/// Parses a string for variable terms and replaces each occurrence accordingly
	/// </summary>
	/// <param name="text">The string to parse</param>
	/// <param name="resolveCallback">A callback method that supplies variable values.</param>
	/// <returns></returns>
	public static string ParseAndResolveVariables(string text, Func<StringName, string> resolveCallback)
	{
		ReadOnlySpan<char> characters = text;

		// Naive variable usage test
		if (!characters.Contains('$'))
			return text;

		Span<char> tmpString = stackalloc char[MaxDialogueLineLength];
		int strIdx = 0;
		int? startIdx = null;
		int endLen = characters.Length - 1;

		for (int i = 0; i < characters.Length; ++ i) {
			bool isEndOfLine = i == endLen;

			// Look for variable end
			if (startIdx != null &&
				(IsVariableTerminator(characters[i]) || isEndOfLine))
			{
				ReadOnlySpan<char> variableName;
				int sliceLen;

				if (isEndOfLine) {
					sliceLen = endLen - startIdx.Value;
					variableName = characters[startIdx.Value..];
				}
				else {
					sliceLen = i - startIdx.Value;
					variableName = characters.Slice(startIdx.Value, sliceLen);

					i --;
				}

				strIdx -= sliceLen;

				ReadOnlySpan<char> variableValue = resolveCallback.Invoke(variableName.ToString());

				// Overwrite variable name with value
				for (int j = 0; j < variableValue.Length; ++ j) {
					tmpString[strIdx] = variableValue[j];
					strIdx ++;
				}

				startIdx = null;
			}
			// Look for variable start
			else if (startIdx == null && characters[i] == '$') {
				startIdx = i + 1;
			}
			// Copy non-variable characters as-is
			else {
				tmpString[strIdx] = characters[i];
				strIdx ++;
			}
		}

		return tmpString[..strIdx].ToString();
	}

	/// <summary>
	/// Parses a multi-parameter command into a span of strings
	/// </summary>
	/// <param name="parameterStr">The parameter string to parse</param>
	/// <param name="parameters">A span of strings to store the parameters</param>
	public static void ParseCommandParameters(ReadOnlySpan<char> parameterStr, ref Span<string> parameters)
	{
		if (parameterStr.Length < 1)
			return;

		Span<string> tmpParameters = new string[MaxParameterCount];
		int parameterIdx = 0;

		int start = 0;

		for (int i = 0; i < parameterStr.Length; ++ i) {
			if (!char.IsWhiteSpace(parameterStr[i]))
				continue;

			ExtractCommandParameterStr(
				parameter: parameterStr,
				start: start,
				end: i,
				parameters: ref tmpParameters,
				parameterIdx: ref parameterIdx
			);

			start = i + 1;
		}

		if (start < parameterStr.Length) {
			ExtractCommandParameterStr(
				parameter: parameterStr,
				start: start,
				end: parameterStr.Length,
				parameters: ref tmpParameters,
				parameterIdx: ref parameterIdx
			);
		}

		parameters = tmpParameters[..parameterIdx];
	}

	private static void ExtractCommandParameterStr(ReadOnlySpan<char> parameter, int start, int end, ref Span<string> parameters, ref int parameterIdx)
	{
		int sliceLen = end - start;
		ReadOnlySpan<char> slice = parameter.Slice(start, sliceLen);

		parameters[parameterIdx] = slice.ToString();
		parameterIdx ++;
	}
}