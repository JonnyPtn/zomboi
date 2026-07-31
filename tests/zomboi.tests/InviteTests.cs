using zomboi;

namespace zomboi.tests;

public class InviteTests
{
    [Fact]
    public void AsksForEverythingTheBotNeedsAndNothingElse()
    {
        // View Channel, Send Messages, Embed Links and Manage Webhooks, as the readme lists them
        Assert.Equal(536890368ul, Invite.Permissions);
    }

    [Fact]
    public void BuildsTheInviteForAnApplication()
    {
        Assert.Equal(
            "https://discord.com/api/oauth2/authorize?client_id=123456789012345678" +
            "&permissions=536890368&scope=bot%20applications.commands",
            Invite.Url(123456789012345678));
    }
}
