using zomboi;

namespace zomboi.tests;

public class LogLineTests
{
    [Fact]
    public void ReadsTheTimeStampAndMessage()
    {
        Assert.True(LogLine.TryParse("[27-01-24 19:05:12.113] 1201 \"Bob\" fully connected (10680,9282,0)", out var line));
        Assert.Equal(new DateTime(2024, 1, 27, 19, 5, 12, 113), line.TimeStamp);
        Assert.Equal("1201 \"Bob\" fully connected (10680,9282,0)", line.Message);
    }

    [Fact]
    public void KeepsBracketsThatBelongToTheMessage()
    {
        Assert.True(LogLine.TryParse("[27-01-24 19:07:00.001] [1201][Bob][10680,9282,0][Fitness=5]", out var line));
        Assert.Equal("[1201][Bob][10680,9282,0][Fitness=5]", line.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no brackets here at all")]
    [InlineData("[27-01-24 19:05:12.113 unterminated")]
    [InlineData("[not a timestamp] message")]
    [InlineData("[] message")]
    [InlineData("znet: Java_zombie_core_znet_SteamUtils_n_1IsSteamRunning")]
    public void RefusesAnythingThatIsntATimeStampedLine(string line)
    {
        Assert.False(LogLine.TryParse(line, out _));
    }

    [Fact]
    public void SurvivesALineThatIsNothingButATimeStamp()
    {
        Assert.True(LogLine.TryParse("[27-01-24 19:05:12.113]", out var line));
        Assert.Equal(string.Empty, line.Message);
    }
}
