using SadChromaLib.Persistence;

namespace SadChromaLib.Specialisations.Dialogue.Types;

partial class DialogueGraph : ISerialisableComponent
{
    public void Serialise(PersistenceWriter writer) {
        writer.Write(Nodes);
    }

    public void Deserialise(PersistenceReader reader) {
        Nodes = reader.ReadComponents<DialogueNode>();
    }
}