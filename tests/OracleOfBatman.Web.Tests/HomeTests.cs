using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using OracleOfBatman.Domain;
using OracleOfBatman.Graph;
using OracleOfBatman.Graph.ComicVine;
using OracleOfBatman.Web.Components.Pages;
using OracleOfBatman.Web.Tests.Fakes;

namespace OracleOfBatman.Web.Tests;

public class HomeTests : BunitContext, IAsyncLifetime
{
  public HomeTests()
  {
    Services.AddMudServices();
    JSInterop.SetupVoid("mudPopover.initialize", _ => true);
    JSInterop.SetupVoid("mudElementRef.addOnBlurEvent", _ => true);
    JSInterop.SetupVoid("mudPopover.connect", _ => true);
    JSInterop.SetupVoid("mudPointerEventsNone.dispose").SetVoidResult();
    JSInterop.SetupVoid("mudPopover.dispose").SetVoidResult();
    JSInterop.Setup<int>("mudpopoverHelper.countProviders");
  }

  public Task InitializeAsync() => Task.CompletedTask;

  public new async Task DisposeAsync() => await base.DisposeAsync();

  [Fact]
  public void DefaultsCharacterBToBatman_WhenBatmanAlreadyExistsInTheGraph()
  {
    var batman = new Character(1699, "Batman", null, "https://comicvine.gamespot.com/batman/4005-1699/");
    Services.AddSingleton<IGraphStore>(new StubGraphStore(batman));

    Render<MudPopoverProvider>();
    var cut = Render<Home>();

    Assert.Contains("Batman", cut.Markup);
  }

  [Fact]
  public void LeavesCharacterBEmpty_WhenBatmanIsNotInTheGraphYet()
  {
    Services.AddSingleton<IGraphStore>(new StubGraphStore());

    Render<MudPopoverProvider>();
    var cut = Render<Home>();

    // Note: Home includes the page title, Oracle of Batman.
    // We want to assert that the markup does not contain "Batman", otherwise.
    Assert.DoesNotContain("Batman", cut.Markup.Replace("Oracle of Batman", string.Empty));
  }

  [Fact]
  public void RendersDataTestIdsForTheAlwaysVisibleInteractiveElements()
  {
    Services.AddSingleton<IGraphStore>(new StubGraphStore());
    Services.AddSingleton<IComicVineCharacterSearchSource>(new StubComicVineCharacterSearchSource());

    Render<MudPopoverProvider>();
    var cut = Render<Home>();

    Assert.NotEmpty(cut.FindAll("[data-testid='character-a-input']"));
    Assert.NotEmpty(cut.FindAll("[data-testid='character-b-input']"));
    Assert.NotEmpty(cut.FindAll("[data-testid='go-button']"));
    Assert.NotEmpty(cut.FindAll("[data-testid='comic-vine-search-input']"));
    Assert.NotEmpty(cut.FindAll("[data-testid='comic-vine-search-button']"));
  }
}
