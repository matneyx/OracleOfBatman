using System.Text;
using OracleOfBatman.Ingest.ComicVine;
using Xunit;

namespace OracleOfBatman.Ingest.Tests;

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

    private static MemoryStream ToStream(string xml) => new(Encoding.UTF8.GetBytes(xml));
}
