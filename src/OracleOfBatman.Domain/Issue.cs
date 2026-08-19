namespace OracleOfBatman.Domain;

public sealed record Issue
{
  public Issue(int comicVineId,
    string? name,
    int pathUseCount = 0,
    int[]? characterCredits = null,
    DateTime? ingestionDateTime = null,
    string? imageUrl = null,
    string? siteDetailUrl = null,
    int? volumeId = null,
    string? volumeName = null)
  {
    ComicVineId = comicVineId;
    Name = name;
    ImageUrl = imageUrl;
    SiteDetailUrl = siteDetailUrl;
    VolumeId = volumeId;
    VolumeName = volumeName;
    CharacterCredits = characterCredits ?? [];
    PathUseCount = pathUseCount;
    IngestionDateTime = ingestionDateTime;
  }

  // Always shows the Volume when known (ADR-0015) — "TPB" needs no special-casing here,
  // it's just a normal Name that gets combined like any other.
  public string ToDisplayName()
  {
    var hasName = !string.IsNullOrEmpty(Name);

    if (VolumeName is not null)
    {
      return hasName ? $"{VolumeName}: {Name}" : VolumeName;
    }

    return hasName ? Name! : string.Empty;
  }

  public int ComicVineId { get; init; }

  public string? Name { get; set; }

  public string? ImageUrl { get; set; }

  public string? SiteDetailUrl { get; set; }

  public int? VolumeId { get; set; }

  public string? VolumeName { get; set; }
  public DateTime? IngestionDateTime { get; set; }
  public int[] CharacterCredits { get; set; } = [];
  public int PathUseCount { get; set; }
}
