using System.Xml.Serialization;

namespace OracleOfBatman.Graph.ComicVine;

public static class ComicVineXmlReader
{
  private static readonly XmlSerializer CharacterSerializer = new(typeof(ComicVineCharacterEnvelope));
  private static readonly XmlSerializer IssueSerializer = new(typeof(ComicVineIssueEnvelope));
  private static readonly XmlSerializer SearchSerializer = new(typeof(ComicVineSearchEnvelope));

  public static ComicVineCharacter ReadCharacter(string filePath)
  {
    using var stream = File.OpenRead(filePath);
    return ReadCharacter(stream);
  }

  public static ComicVineCharacter ReadCharacter(Stream xml)
  {
    var envelope = (ComicVineCharacterEnvelope)CharacterSerializer.Deserialize(xml)!;
    return envelope.Results;
  }

  public static ComicVineIssue ReadIssue(string filePath)
  {
    using var stream = File.OpenRead(filePath);
    return ReadIssue(stream);
  }

  public static ComicVineIssue ReadIssue(Stream xml)
  {
    var envelope = (ComicVineIssueEnvelope)IssueSerializer.Deserialize(xml)!;
    return envelope.Results;
  }

  public static IReadOnlyList<ComicVineSearchCharacterResult> ReadSearchResults(Stream xml)
  {
    var envelope = (ComicVineSearchEnvelope)SearchSerializer.Deserialize(xml)!;
    return envelope.Results;
  }
}
