using zomboi;

namespace zomboi.tests;

public class ConfigListTests
{
    /// <summary>
    /// A cut down servertest.ini, keeping a key that starts with one of the ones we edit so we'd notice
    /// if the lookup went back to matching on any part of a line.
    /// </summary>
    private static string[] Config() => new[]
    {
        "PVP=true",
        "DefaultPort=16261",
        "Mods=",
        "ModsRequiredMessage=",
        "WorkshopItems=",
    };

    [Fact]
    public void AddsToAnEmptyList()
    {
        var lines = Config();
        Assert.True(ConfigList.TryAdd(lines, "Mods", "BetterSorting", out var added));
        Assert.True(added);
        Assert.Equal("Mods=BetterSorting", lines[2]);
    }

    [Fact]
    public void AddsToAListThatAlreadyHasEntries()
    {
        var lines = Config();
        ConfigList.TryAdd(lines, "WorkshopItems", "2169435993", out _);
        Assert.True(ConfigList.TryAdd(lines, "WorkshopItems", "2392709985", out var added));
        Assert.True(added);
        Assert.Equal("WorkshopItems=2169435993,2392709985", lines[4]);
    }

    [Fact]
    public void LeavesAnEntryThatIsAlreadyThere()
    {
        var lines = Config();
        ConfigList.TryAdd(lines, "Mods", "BetterSorting", out _);
        var before = lines[2];

        Assert.True(ConfigList.TryAdd(lines, "Mods", "BetterSorting", out var added));
        Assert.False(added);
        Assert.Equal(before, lines[2]);
    }

    [Fact]
    public void TellsApartAnEntryThatIsOnlyAPrefixOfAnother()
    {
        var lines = Config();
        ConfigList.TryAdd(lines, "Mods", "Firearms", out _);

        Assert.True(ConfigList.TryAdd(lines, "Mods", "Fire", out var added));
        Assert.True(added);
        Assert.Equal("Mods=Firearms,Fire", lines[2]);
    }

    [Fact]
    public void DoesntTouchAKeyThatOnlyStartsTheSame()
    {
        var lines = Config();
        ConfigList.TryAdd(lines, "Mods", "BetterSorting", out _);
        Assert.Equal("ModsRequiredMessage=", lines[3]);
    }

    [Fact]
    public void FailsOnAKeyTheFileDoesntHave()
    {
        var lines = Config();
        Assert.False(ConfigList.TryAdd(lines, "WorkshopItemsMissing", "1", out var added));
        Assert.False(added);
    }

    [Fact]
    public void LeavesTheRestOfTheFileAlone()
    {
        var lines = Config();
        ConfigList.TryAdd(lines, "Mods", "BetterSorting", out _);
        Assert.Equal("PVP=true", lines[0]);
        Assert.Equal("DefaultPort=16261", lines[1]);
        Assert.Equal("WorkshopItems=", lines[4]);
    }

    [Fact]
    public void ReadsAValue()
    {
        Assert.True(ConfigList.TryReadValue(Config(), "DefaultPort", out var value));
        Assert.Equal("16261", value);
    }

    [Fact]
    public void ReadsAnEmptyValue()
    {
        Assert.True(ConfigList.TryReadValue(Config(), "Mods", out var value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void FailsToReadAValueThatIsntThere()
    {
        Assert.False(ConfigList.TryReadValue(Config(), "SteamPort1", out _));
    }
}
