using Discord;
using Discord.WebSocket;
using System.Numerics;

namespace zomboi
{
    public class Playerlistener : LogFileListener
    {
        private IMessageChannel? m_channel;
        private readonly Server m_server;
        public Playerlistener(Server server) : base("*user.txt")
        {
            m_server = server;
            m_server.OnPlayerJoined += OnPlayerAdded;
            m_server.OnPlayerDied += OnPlayerDeath;
        }

        public async Task OnPlayerAdded(Player player)
        {
            if (m_channel == null)
            {
                Logger.Warn("Player notification channel not set");
                return;
            }
            // Where they came in is worth a look, so link it on the map when the log gave us one
            var where = player.HasPosition ? $" {Map.Link(player.Position)}" : string.Empty;
            // How long they've lasted comes from the perk log, which writes it just after the line that
            // got us here, so it's worth holding the notification back for
            var hours = await m_server.WaitForHoursSurvived(player);
            var survived = hours == null ? string.Empty : $", {Survival.Describe(hours.Value)} survived";
            await m_channel.SendMessageAsync($":wave: {player.Name} has joined{where}{survived}");
        }

        public async Task OnPlayerDeath(Player player)
        {
            if (m_channel == null)
            {
                Logger.Warn("Player notification channel not set");
                return;
            }
            // Where they fell is the part worth looking at, and it's on the same log line the death
            // came from, along with how long they'd lasted - so unlike a join there's nothing to wait for
            var where = player.HasPosition ? $" at {Map.Link(player.Position)}" : string.Empty;
            var survived = player.HoursSurvived == null
                ? string.Empty
                : $", {Survival.Describe(player.HoursSurvived.Value)} survived";
            await m_channel.SendMessageAsync($":skull: {player.Name} died{where}{survived}");
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            m_channel = Channels.Find(client, channelID, "user");
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            if (m_channel == null)
            {
                return false;
            }

            if (line.Message.Contains("fully connected"))
            {
                if (!LogFields.TryReadQuotedName(line.Message, out var name))
                {
                    Logger.Warn($"No player name in: {line.Message}");
                    return false;
                }

                // The position is in parentheses like (x,y,...), not sure what the last param is... floor?
                Vector2? position = LogFields.TryReadPosition(line.Message, out var parsed) ? parsed : null;
                await m_server.PlayerConnected(name, line.TimeStamp, position);
                return true;
            }
            else if (line.Message.Contains("disconnected"))
            {
                if (!LogFields.TryReadQuotedName(line.Message, out var name))
                {
                    Logger.Warn($"No player name in: {line.Message}");
                    return false;
                }

                // The disconnect line carries a position too, which is the last one we'll get for them
                Vector2? leftAt = LogFields.TryReadPosition(line.Message, out var lastSeen) ? lastSeen : null;
                if (m_server.PlayerDisconnected(name, line.TimeStamp, leftAt))
                {
                    await m_channel.SendMessageAsync($":person_running: {name} has left");
                }
                return true;
            }
            else if (line.Message.Contains("died"))
            {
                // Handled, but from the perk log rather than here - see PerkListener. This line knows
                // whether it was a pvp death and that one doesn't, so it's the one to come back to if
                // that's ever worth reporting.
                return true;
            }
            return false;
        }
    }
}
