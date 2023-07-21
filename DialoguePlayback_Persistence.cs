using Godot;
using Godot.Collections;

namespace SadChromaLib.Dialogue;

using SerialisedData = Dictionary<StringName, Variant>;

// Note: As for now, this persistence component
// only serialises instance variables and nothing more.
public sealed partial class DialoguePlayback
{
	private const string KeyVariables = "variables";

	public SerialisedData Serialise()
	{
		return new() {
			[KeyVariables] = _scriptVariables
		};
	}

	public void Deserialise(SerialisedData data)
	{
		if (data.ContainsKey(KeyVariables)) {
			_scriptVariables = (SerialisedData) data[KeyVariables];
		}
	}
}