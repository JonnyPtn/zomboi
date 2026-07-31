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
        }

        public async Task OnPlayerAdded(Player player)
        {
            if (m_channel == null)
            {
                Logger.Warn("Player notification channel not set");
                return;
            }
            await m_channel.SendMessageAsync($":wave: {player.Name} has joined");
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

                if (m_server.PlayerDisconnected(name, line.TimeStamp))
                {
                    await m_channel.SendMessageAsync($":person_running: {name} has left");
                }
                return true;
            }
            return false;
        }
    }
}
