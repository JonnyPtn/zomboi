using System.Numerics;
using zomboi;

namespace zomboi.tests;

public class MapTests
{
    [Fact]
    public void PointsTheMapAtAPosition()
    {
        Assert.Equal("https://map.projectzomboid.com/?10637x10267", Map.Url(new Vector2(10637, 10267)));
    }

    [Fact]
    public void RoundsAFractionalPosition()
    {
        Assert.Equal("https://map.projectzomboid.com/?10638x10267", Map.Url(new Vector2(10637.6f, 10267.2f)));
    }

    [Fact]
    public void PutsThePinOutsideTheLinkDiscordWontWrapAroundIt()
    {
        Assert.Equal("\U0001F4CD[map](https://map.projectzomboid.com/?10637x10267)",
            Map.Link(new Vector2(10637, 10267)));
    }
}
