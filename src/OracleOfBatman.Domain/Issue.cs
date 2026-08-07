namespace OracleOfBatman.Domain;

public sealed record Issue(
  int ComicVineId,
  string? Name,
  string? ImageUrl = null,
  string? SiteDetailUrl = null,
  int? VolumeId = null,
  string? VolumeName = null)
{
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

  public int ComicVineId { get; init; } = ComicVineId;

  public string? Name { get; set; } = Name;

  public string? ImageUrl { get; set; } = ImageUrl;

  public string? SiteDetailUrl { get; set; } = SiteDetailUrl;

  public int? VolumeId { get; set; } = VolumeId;

  public string? VolumeName { get; set; } = VolumeName;
}
