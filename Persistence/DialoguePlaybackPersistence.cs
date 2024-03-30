using SadChromaLib.Persistence;

namespace SadChromaLib.Specialisations.Dialogue;

public sealed partial class DialoguePlayback: ISerialisableComponent
{
	public void Serialise(PersistenceWriter writer)
	{
		writer.Write(_scriptVariables);
	}

	public void Deserialise(PersistenceReader reader)
	{
		_scriptVariables = reader.ReadDataDict();
	}
}