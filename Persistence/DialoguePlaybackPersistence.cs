using Godot;
using Godot.Collections;

using SadChromaLib.Persistence;

namespace SadChromaLib.Specialisations.Dialogue;

using SerialisedData = Dictionary<StringName, Variant>;

public sealed partial class DialoguePlayback: ISerialisableComponent
{
	private static StringName KeyVariables => "variables";

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