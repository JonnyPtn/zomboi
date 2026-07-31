using Discord;
using Discord.WebSocket;

namespace zomboi
{
    /// <summary>
    /// The channels the bot posts its notifications to, as listed by ID in zomboi.ini.
    /// </summary>
    public static class Channels
    {
        /// <summary>
        /// Resolve one of the channel IDs from zomboi.ini, saying what to check if we can't.
        /// </summary>
        /// <param name="description">What the channel is for, e.g. "chat", used in the error messages</param>
        public static IMessageChannel? Find(DiscordSocketClient client, string channelID, string description)
        {
            if (string.IsNullOrWhiteSpace(channelID))
            {
                Logger.Warn($"No {description} channel set in zomboi.ini, so those notifications are off");
                return null;
            }
            if (!ulong.TryParse(channelID.Trim(), out var id))
            {
                Logger.Error($"'{channelID}' is not a valid {description} channel ID, check the {description} " +
                    "channel value in zomboi.ini");
                return null;
            }
            if (client.GetChannel(id) is not IMessageChannel channel)
            {
                Logger.Error($"Unable to find {description} channel {channelID}. Check the ID in zomboi.ini, and " +
                    "that the bot has been invited to that server and has the View Channel permission on it.");
                return null;
            }
            return channel;
        }
    }
}
