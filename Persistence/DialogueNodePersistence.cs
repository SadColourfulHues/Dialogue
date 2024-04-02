using SadChromaLib.Persistence;

namespace SadChromaLib.Specialisations.Dialogue.Types;

partial struct DialogueNode : ISerialisableComponent
{
    public void Serialise(PersistenceWriter writer)
    {
        writer.Write(Tag);
        writer.Write(CharacterId);
        writer.Write(DialogueText);

        writer.Write(CommandList);
        writer.Write(Choices);
    }

    public void Deserialise(PersistenceReader reader)
    {
        Tag = reader.ReadString();
        CharacterId = reader.ReadString();
        DialogueText = reader.ReadString();

        CommandList = reader.ReadComponents<DialogueCommand>();
        Choices = reader.ReadComponents<DialogueChoice>();
    }
}

partial struct DialogueChoice : ISerialisableComponent
{
    public void Serialise(PersistenceWriter writer)
    {
        writer.Write(ChoiceText);
        writer.Write(TargetTag);
    }

    public void Deserialise(PersistenceReader reader)
    {
        ChoiceText = reader.ReadString();
        TargetTag = reader.ReadString();
    }
}

partial struct DialogueCommand : ISerialisableComponent
{
    public void Serialise(PersistenceWriter writer)
    {
        writer.Write(Name);
        writer.Write(Parameters);
    }

    public void Deserialise(PersistenceReader reader)
    {
        Name = reader.ReadString();
        Parameters = reader.ReadStringArray();
    }
}