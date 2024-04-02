using SadChromaLib.Persistence;

namespace SadChromaLib.Specialisations.Dialogue.Playback;

partial class DialoguePlayback: ISerialisableComponent
{
	public void Serialise(PersistenceWriter writer)
	{
		writer.Write(_scriptVariables);
	}

	public void Deserialise(PersistenceReader reader)
	{
		reader.ReadDataDict(_scriptVariables);
	}
}