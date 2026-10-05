using MudBlazor;

namespace OracleOfBatman.Web.Components.Layout;

/// <summary>
///   Dark-only palette: charcoal surfaces, Bat-Signal yellow for the one thing on screen
///   that matters (primary actions, the Batman Number), cold steel blue for everything
///   secondary. There is deliberately no light palette — the layout forces dark mode.
/// </summary>
public static class GothamTheme
{
  private static readonly string[] DisplayFont = ["Oswald", "Roboto Condensed", "sans-serif"];
  private static readonly string[] BodyFont = ["Inter", "Roboto", "sans-serif"];

  public static readonly MudTheme Theme = new()
  {
    PaletteDark = new PaletteDark
    {
      Primary = "#F5C518",
      PrimaryContrastText = "#0B0D10",
      Secondary = "#6F8FAF",
      Tertiary = "#9B2C2C",
      Background = "#0B0D10",
      Surface = "#14181E",
      AppbarBackground = "#0B0D10",
      AppbarText = "#F5C518",
      DrawerBackground = "#14181E",
      TextPrimary = "#E6E8EB",
      TextSecondary = "#9AA3AE",
      ActionDefault = "#9AA3AE",
      LinesDefault = "#262C35",
      LinesInputs = "#3A424D",
      TableLines = "#262C35",
      TableHover = "#1B2028",
      Divider = "#262C35",
      Info = "#6F8FAF",
      Success = "#4E9F6D",
      Warning = "#F5C518",
      Error = "#C0453F",
    },
    Typography = new Typography
    {
      Default = new DefaultTypography { FontFamily = BodyFont },
      H1 = new H1Typography { FontFamily = DisplayFont, FontWeight = "600", LetterSpacing = ".04em" },
      H2 = new H2Typography { FontFamily = DisplayFont, FontWeight = "600", LetterSpacing = ".04em" },
      H3 = new H3Typography { FontFamily = DisplayFont, FontWeight = "600", LetterSpacing = ".04em" },
      H4 = new H4Typography { FontFamily = DisplayFont, FontWeight = "500", LetterSpacing = ".04em" },
      H5 = new H5Typography { FontFamily = DisplayFont, FontWeight = "500", LetterSpacing = ".03em" },
      H6 = new H6Typography { FontFamily = DisplayFont, FontWeight = "500", LetterSpacing = ".08em" },
      Button = new ButtonTypography { FontFamily = DisplayFont, FontWeight = "500", LetterSpacing = ".1em" },
    },
    LayoutProperties = new LayoutProperties { DefaultBorderRadius = "6px" },
  };
}
