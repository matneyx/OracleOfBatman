using System.Text;
using OracleOfBatman.Graph.ComicVine;
using Xunit;

namespace OracleOfBatman.Graph.Tests;

public class ComicVineXmlReaderTests
{
    // Trimmed to the fields the crawl actually reads — see ComicVineXml.cs — not a full
    // real Comic Vine response.
    private const string CharacterXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <response>
            <results>
                <id>157242</id>
                <name><![CDATA[Jeff the Land Shark]]></name>
                <site_detail_url><![CDATA[https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/]]></site_detail_url>
                <image>
                    <icon_url><![CDATA[https://example.com/jeff-icon.jpg]]></icon_url>
                </image>
                <character_friends>
                    <character>
                        <id>1475</id>
                        <name><![CDATA[Hawkeye]]></name>
                    </character>
                </character_friends>
                <character_enemies>
                    <character>
                        <id>196168</id>
                        <name><![CDATA[Ken the Septapus]]></name>
                    </character>
                </character_enemies>
                <issue_credits>
                    <issue>
                        <id>1175698</id>
                        <name><![CDATA[Beach Bashed!]]></name>
                        <site_detail_url><![CDATA[https://comicvine.gamespot.com/some-issue/4000-1175698/]]></site_detail_url>
                    </issue>
                </issue_credits>
            </results>
        </response>
        """;

    private const string IssueXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <response>
            <results>
                <id>1101757</id>
                <name><![CDATA[Spoonful of Everything – Part 2!]]></name>
                <cover_date><![CDATA[2025-04-04]]></cover_date>
                <image>
                    <icon_url><![CDATA[https://example.com/issue-icon.jpg]]></icon_url>
                </image>
                <character_credits>
                    <character>
                        <id>125054</id>
                        <name><![CDATA[Gwenpool]]></name>
                    </character>
                    <character>
                        <id>157242</id>
                        <name><![CDATA[Jeff the Land Shark]]></name>
                    </character>
                </character_credits>
                <volume>
                    <id>139047</id>
                    <name><![CDATA[It's Jeff Infinity Comic]]></name>
                    <site_detail_url><![CDATA[https://comicvine.gamespot.com/its-jeff-infinity-comic/4050-139047/]]></site_detail_url>
                </volume>
            </results>
        </response>
        """;

    [Fact]
    public void ReadCharacter_ParsesIdAndName()
    {
        var character = ComicVineXmlReader.ReadCharacter(ToStream(CharacterXml));

        Assert.Equal(157242, character.Id);
        Assert.Equal("Jeff the Land Shark", character.Name);
    }

    [Fact]
    public void ReadCharacter_ParsesSiteDetailUrlAndImage()
    {
        var character = ComicVineXmlReader.ReadCharacter(ToStream(CharacterXml));

        Assert.Equal("https://comicvine.gamespot.com/jeff-the-land-shark/4005-157242/", character.SiteDetailUrl);
        Assert.Equal("https://example.com/jeff-icon.jpg", character.Image?.IconUrl);
    }

    [Fact]
    public void ReadCharacter_ParsesFriendsEnemiesAndIssueCredits()
    {
        var character = ComicVineXmlReader.ReadCharacter(ToStream(CharacterXml));

        var friend = Assert.Single(character.CharacterFriends);
        Assert.Equal(1475, friend.Id);
        Assert.Equal("Hawkeye", friend.Name);

        var enemy = Assert.Single(character.CharacterEnemies);
        Assert.Equal(196168, enemy.Id);

        var issueCredit = Assert.Single(character.IssueCredits);
        Assert.Equal(1175698, issueCredit.Id);
        Assert.Equal("https://comicvine.gamespot.com/some-issue/4000-1175698/", issueCredit.SiteDetailUrl);
    }

    [Fact]
    public void ReadIssue_ParsesIdNameCoverDateAndCharacterCredits()
    {
        var issue = ComicVineXmlReader.ReadIssue(ToStream(IssueXml));

        Assert.Equal(1101757, issue.Id);
        Assert.Equal("2025-04-04", issue.CoverDate);
        Assert.Equal(2, issue.CharacterCredits.Count);
        Assert.Contains(issue.CharacterCredits, c => c.Id == 125054 && c.Name == "Gwenpool");
        Assert.Contains(issue.CharacterCredits, c => c.Id == 157242 && c.Name == "Jeff the Land Shark");
    }

    [Fact]
    public void ReadIssue_ParsesImage()
    {
        var issue = ComicVineXmlReader.ReadIssue(ToStream(IssueXml));

        Assert.Equal("https://example.com/issue-icon.jpg", issue.Image?.IconUrl);
    }

    [Fact]
    public void ReadIssue_ParsesVolume()
    {
        var issue = ComicVineXmlReader.ReadIssue(ToStream(IssueXml));

        Assert.Equal(139047, issue.Volume?.Id);
        Assert.Equal("It's Jeff Infinity Comic", issue.Volume?.Name);
        Assert.Equal("https://comicvine.gamespot.com/its-jeff-infinity-comic/4050-139047/", issue.Volume?.SiteDetailUrl);
    }

    private const string SearchXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <response>
            <results>
                <character>
                    <id>46793</id>
                    <name><![CDATA[BloodRayne]]></name>
                    <site_detail_url><![CDATA[https://comicvine.gamespot.com/bloodrayne/4005-46793/]]></site_detail_url>
                </character>
                <character>
                    <id>12510</id>
                    <name><![CDATA[Brother Blood]]></name>
                    <site_detail_url><![CDATA[https://comicvine.gamespot.com/brother-blood/4005-12510/]]></site_detail_url>
                </character>
            </results>
        </response>
        """;

    [Fact]
    public void ReadSearchResults_ParsesFlatListOfCharacters()
    {
        var results = ComicVineXmlReader.ReadSearchResults(ToStream(SearchXml));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == 46793 && r.Name == "BloodRayne" && r.SiteDetailUrl == "https://comicvine.gamespot.com/bloodrayne/4005-46793/");
        Assert.Contains(results, r => r.Id == 12510 && r.Name == "Brother Blood");
    }

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
