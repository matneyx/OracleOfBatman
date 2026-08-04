using System.Xml.Serialization;

namespace OracleOfBatman.Ingest.ComicVine;

public static class ComicVineXmlReader
{
    private static readonly XmlSerializer CharacterSerializer = new(typeof(ComicVineCharacterEnvelope));
    private static readonly XmlSerializer IssueSerializer = new(typeof(ComicVineIssueEnvelope));

    public static ComicVineCharacter ReadCharacter(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var envelope = (ComicVineCharacterEnvelope)CharacterSerializer.Deserialize(stream)!;
        return envelope.Results;
    }

    public static ComicVineIssue ReadIssue(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var envelope = (ComicVineIssueEnvelope)IssueSerializer.Deserialize(stream)!;
        return envelope.Results;
    }
}
