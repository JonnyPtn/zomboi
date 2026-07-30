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

        public async void OnPlayerAdded(Player player)
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
            if (!ulong.TryParse(channelID, out var id))
            {
                Logger.Error($"'{channelID}' is not a valid user channel ID, check the users channel value in zomboi.ini");
                return;
            }
            m_channel = client.GetChannel(id) as IMessageChannel;
            if (m_channel == null)
            {
                Logger.Error($"Unable to find user channel {channelID}. Check the ID in zomboi.ini, and that the " +
                    "bot has been invited to that server and has the View Channel permission on it.");
            }
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            if (m_channel == null)
            {
                Logger.Warn("Player notification channel not set");
                return false;
            }
            if (line.Message.Contains("fully connected"))
            {
                // The only part of this line in quotes should be the player name, so find that
                var firstQuote = line.Message.IndexOf("\"");
                var lastQuote = line.Message.LastIndexOf("\"");
                var name = line.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                // And the position will be in parentheses like (x,y,...) not sure what the last param is... floor?
                var firstParen = line.Message.IndexOf("(");
                var lastComma = line.Message.LastIndexOf(",");
                var positionString = line.Message.Substring(firstParen + 1, lastComma - firstParen - 1);
                var positions = positionString.Split(',');
                var position = new Vector2(int.Parse(positions[0]), int.Parse(positions[1]));

                var player = m_server.GetOrCreatePlayer(name, line.TimeStamp);
                player.Online = true;
                player.Position = position;
                if (player.LastSeen < line.TimeStamp)
                {
                    player.LastSeen = line.TimeStamp;
                    return true;
                }
            }
            else if (line.Message.Contains("disconnected"))
            {
                // The only part of this line in quotes should be the player name, so find that
                var firstQuote = line.Message.IndexOf("\"");
                var lastQuote = line.Message.LastIndexOf("\"");
                var name = line.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                var player = m_server.GetOrCreatePlayer(name, line.TimeStamp);
                if (player.LastSeen < line.TimeStamp)
                {
                    player.LastSeen = line.TimeStamp;
                    if (player.Online)
                    {
                        player.Online = false;
                        await m_channel.SendMessageAsync($":person_running: {player.Name} has left");
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
