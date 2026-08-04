using System.Xml.Serialization;

namespace OracleOfBatman.Ingest.ComicVine;

// Only the fields the crawl actually needs are mapped — Comic Vine's real responses carry
// far more (images, creators, description, ...) that XmlSerializer silently ignores.

[XmlRoot("response")]
public sealed class ComicVineCharacterEnvelope
{
    [XmlElement("results")]
    public ComicVineCharacter Results { get; set; } = null!;
}

public sealed class ComicVineCharacter
{
    [XmlElement("id")]
    public int Id { get; set; }

    [XmlElement("name")]
    public string Name { get; set; } = "";

    [XmlArray("character_friends")]
    [XmlArrayItem("character")]
    public List<ComicVineCharacterRef> CharacterFriends { get; set; } = [];

    [XmlArray("character_enemies")]
    [XmlArrayItem("character")]
    public List<ComicVineCharacterRef> CharacterEnemies { get; set; } = [];

    [XmlArray("issue_credits")]
    [XmlArrayItem("issue")]
    public List<ComicVineIssueRef> IssueCredits { get; set; } = [];
}

public sealed class ComicVineCharacterRef
{
    [XmlElement("id")]
    public int Id { get; set; }

    [XmlElement("name")]
    public string Name { get; set; } = "";
}

public sealed class ComicVineIssueRef
{
    [XmlElement("id")]
    public int Id { get; set; }

    [XmlElement("name")]
    public string? Name { get; set; }
}

[XmlRoot("response")]
public sealed class ComicVineIssueEnvelope
{
    [XmlElement("results")]
    public ComicVineIssue Results { get; set; } = null!;
}

public sealed class ComicVineIssue
{
    [XmlElement("id")]
    public int Id { get; set; }

    [XmlElement("name")]
    public string? Name { get; set; }

    [XmlElement("cover_date")]
    public string? CoverDate { get; set; }

    [XmlArray("character_credits")]
    [XmlArrayItem("character")]
    public List<ComicVineCharacterRef> CharacterCredits { get; set; } = [];
}
