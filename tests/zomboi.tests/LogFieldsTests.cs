using System.Numerics;
using zomboi;

namespace zomboi.tests;

public class BracketedTests
{
    [Fact]
    public void ReadsEveryFieldOfAPerkLine()
    {
        var fields = LogFields.Bracketed("[1201055268][Bob][10680,9282,0][Fitness=5,Strength=4]");
        Assert.Equal(new[] { "1201055268", "Bob", "10680,9282,0", "Fitness=5,Strength=4" }, fields);
    }

    [Fact]
    public void ReadsALevelChangedLine()
    {
        var fields = LogFields.Bracketed("[1201][Bob][10680,9282,0][Level Changed][Fitness][6]");
        Assert.Equal(6, fields.Count);
        Assert.Equal("Level Changed", fields[3]);
        Assert.Equal("Fitness", fields[4]);
        Assert.Equal("6", fields[5]);
    }

    [Fact]
    public void StopsAtAFieldThatIsNeverClosed()
    {
        var fields = LogFields.Bracketed("[1201][Bob][unterminated");
        Assert.Equal(new[] { "1201", "Bob" }, fields);
    }

    [Fact]
    public void FindsNothingInALineWithoutBrackets()
    {
        Assert.Empty(LogFields.Bracketed("Bob levelled up somehow"));
    }

    [Fact]
    public void KeepsEmptyFields()
    {
        Assert.Equal(new[] { "1201", "", "Bob" }, LogFields.Bracketed("[1201][][Bob]"));
    }
}

public class TryReadFieldTests
{
    private const string ChatLine = "Got message: ChatMessage{chat=General, author='Bob', text='hello world'}";

    [Fact]
    public void ReadsTheAuthorOfAChatMessage()
    {
        Assert.True(LogFields.TryReadField(ChatLine, "author='", "'", out var author));
        Assert.Equal("Bob", author);
    }

    [Fact]
    public void ReadsTheBodyOfAChatMessage()
    {
        Assert.True(LogFields.TryReadField(ChatLine, "text='", "'}", out var text, last: true));
        Assert.Equal("hello world", text);
    }

    [Fact]
    public void KeepsAQuoteThatIsPartOfTheMessage()
    {
        var line = "Got message: ChatMessage{chat=General, author='Bob', text='it's fine'}";
        Assert.True(LogFields.TryReadField(line, "text='", "'}", out var text, last: true));
        Assert.Equal("it's fine", text);
    }

    [Fact]
    public void FailsWithoutTheOpener()
    {
        Assert.False(LogFields.TryReadField("Got message: nothing useful", "text='", "'}", out _));
    }

    [Fact]
    public void FailsWithoutTheCloser()
    {
        Assert.False(LogFields.TryReadField("Got message: text='truncated", "text='", "'}", out _));
    }

    [Fact]
    public void FailsWhenTheOnlyCloserComesFirst()
    {
        Assert.False(LogFields.TryReadField("'} then text='", "text='", "'}", out _));
    }
}

public class TryReadQuotedNameTests
{
    [Fact]
    public void ReadsThePlayerNameFromAConnectLine()
    {
        Assert.True(LogFields.TryReadQuotedName("1201055268 \"Bob\" fully connected (10680,9282,0)", out var name));
        Assert.Equal("Bob", name);
    }

    [Fact]
    public void KeepsSpacesInTheName()
    {
        Assert.True(LogFields.TryReadQuotedName("1201 \"Bob the Builder\" disconnected", out var name));
        Assert.Equal("Bob the Builder", name);
    }

    [Theory]
    [InlineData("1201 fully connected")]
    [InlineData("1201 \"unclosed fully connected")]
    [InlineData("1201 \"\" fully connected")]
    public void FailsWithoutAName(string message)
    {
        Assert.False(LogFields.TryReadQuotedName(message, out _));
    }
}

public class TryReadPositionTests
{
    [Fact]
    public void ReadsWholeCoordinates()
    {
        Assert.True(LogFields.TryReadPosition("\"Bob\" fully connected (10680,9282,0)", out var position));
        Assert.Equal(new Vector2(10680, 9282), position);
    }

    [Fact]
    public void ReadsFractionalCoordinates()
    {
        Assert.True(LogFields.TryReadPosition("\"Bob\" fully connected (10680.5,9282.25,0)", out var position));
        Assert.Equal(new Vector2(10680.5f, 9282.25f), position);
    }

    [Fact]
    public void ReadsAPositionThatIsNeverClosed()
    {
        Assert.True(LogFields.TryReadPosition("\"Bob\" fully connected (10680,9282", out var position));
        Assert.Equal(new Vector2(10680, 9282), position);
    }

    [Theory]
    [InlineData("\"Bob\" fully connected")]
    [InlineData("\"Bob\" fully connected (10680)")]
    [InlineData("\"Bob\" fully connected (east,north,0)")]
    public void FailsWithoutTwoCoordinates(string message)
    {
        Assert.False(LogFields.TryReadPosition(message, out _));
    }
}

public class TryReadCoordinatesTests
{
    [Fact]
    public void ReadsThePositionFieldOfAPerkLine()
    {
        Assert.True(LogFields.TryReadCoordinates("10637,10267,0", out var position));
        Assert.Equal(new Vector2(10637, 10267), position);
    }

    [Fact]
    public void IgnoresSpacesBetweenTheCoordinates()
    {
        Assert.True(LogFields.TryReadCoordinates("10637, 10267, 0", out var position));
        Assert.Equal(new Vector2(10637, 10267), position);
    }

    [Theory]
    [InlineData("")]
    [InlineData("10637")]
    [InlineData("Login")]
    public void FailsWithoutTwoCoordinates(string coordinates)
    {
        Assert.False(LogFields.TryReadCoordinates(coordinates, out _));
    }
}

public class TryReadHoursSurvivedTests
{
    [Fact]
    public void ReadsTheHoursOffALoginLine()
    {
        Assert.True(LogFields.TryReadHoursSurvived(
            "[76561198021332810][Bob][10637,10267,0][Login][Hours Survived: 33].", out var hours));
        Assert.Equal(33, hours);
    }

    [Fact]
    public void ReadsTheHoursOffALevelChangedLine()
    {
        Assert.True(LogFields.TryReadHoursSurvived(
            "[76561][Bob][10637,10267,0][Level Changed][Woodwork][2][Hours Survived: 35].", out var hours));
        Assert.Equal(35, hours);
    }

    [Fact]
    public void ReadsAFreshCharacter()
    {
        Assert.True(LogFields.TryReadHoursSurvived(
            "[76561][Bob][10919,10132,0][Created Player 1][Hours Survived: 0].", out var hours));
        Assert.Equal(0, hours);
    }

    [Theory]
    [InlineData("[76561][Bob][10925,10054,0][SRJ START READING].")]
    [InlineData("[76561][Bob][10925,10054,0][Hours Survived: many].")]
    [InlineData("[76561][Bob][10925,10054,0][Hours Survived: 33")]
    public void FailsWithoutAnHourCount(string message)
    {
        Assert.False(LogFields.TryReadHoursSurvived(message, out _));
    }
}
