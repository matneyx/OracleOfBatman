using System.Xml.Serialization;

namespace OracleOfBatman.Graph.ComicVine;

// Only the fields the crawl actually needs are mapped — Comic Vine's real responses carry
// far more (creators, description, ...) that XmlSerializer silently ignores.

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

  [XmlElement("site_detail_url")]
  public string? SiteDetailUrl { get; set; }

  [XmlElement("image")]
  public ComicVineImage? Image { get; set; }

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

public sealed class ComicVineImage
{
  [XmlElement("icon_url")]
  public string? IconUrl { get; set; }
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

  [XmlElement("site_detail_url")]
  public string? SiteDetailUrl { get; set; }
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

  [XmlElement("site_detail_url")]
  public string? SiteDetailUrl { get; set; }

  [XmlElement("cover_date")]
  public string? CoverDate { get; set; }

  [XmlElement("image")]
  public ComicVineImage? Image { get; set; }

  [XmlArray("character_credits")]
  [XmlArrayItem("character")]
  public List<ComicVineCharacterRef> CharacterCredits { get; set; } = [];

  [XmlElement("volume")]
  public ComicVineVolume? Volume { get; set; }
}

/// <summary>
///   The series an issue belongs to. Collected-edition issues (TPBs, omnibuses) often
///   have a blank or generic "TPB" issue name — the Volume's name (the series title) is the only
///   identifying info Comic Vine gives for those.
/// </summary>
public sealed class ComicVineVolume
{
  [XmlElement("id")]
  public int Id { get; set; }

  [XmlElement("name")]
  public string? Name { get; set; }

  [XmlElement("site_detail_url")]
  public string? SiteDetailUrl { get; set; }
}

// The /search/ endpoint's <results> is a flat list of <character> elements directly (not a
// single nested object like the character/issue detail endpoints above).
[XmlRoot("response")]
public sealed class ComicVineSearchEnvelope
{
  [XmlArray("results")]
  [XmlArrayItem("character")]
  public List<ComicVineSearchCharacterResult> Results { get; set; } = [];
}

/// <summary>
///   A /search/?resources=character result. Comic Vine's search is full-text across
///   bio/description too, not a pure name match — expect some tangentially-related results.
/// </summary>
public sealed class ComicVineSearchCharacterResult
{
  [XmlElement("id")]
  public int Id { get; set; }

  [XmlElement("name")]
  public string Name { get; set; } = "";

  [XmlElement("site_detail_url")]
  public string? SiteDetailUrl { get; set; }

  [XmlElement("image")]
  public ComicVineImage? Image { get; set; }
}
