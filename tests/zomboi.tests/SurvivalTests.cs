using zomboi;

namespace zomboi.tests;

public class SurvivalTests
{
    [Theory]
    [InlineData(1, "1 hour")]
    [InlineData(9, "9 hours")]
    [InlineData(23, "23 hours")]
    public void CountsHoursOnTheFirstDay(int hours, string expected)
    {
        Assert.Equal(expected, Survival.Describe(hours));
    }

    [Theory]
    [InlineData(24, "1 day")]
    [InlineData(48, "2 days")]
    [InlineData(240, "10 days")]
    public void LeavesOutTheHoursOnAWholeNumberOfDays(int hours, string expected)
    {
        Assert.Equal(expected, Survival.Describe(hours));
    }

    [Theory]
    [InlineData(25, "1 day 1 hour")]
    [InlineData(33, "1 day 9 hours")]
    [InlineData(250, "10 days 10 hours")]
    public void CountsDaysAndHoursTogether(int hours, string expected)
    {
        Assert.Equal(expected, Survival.Describe(hours));
    }

    [Fact]
    public void DoesntCallAFreshCharacterZero()
    {
        Assert.Equal("under an hour", Survival.Describe(0));
    }
}
