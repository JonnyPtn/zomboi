using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Specialized;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace zomboi
{
    public class Playerlistener : LogFileListener
    {
        private IMessageChannel? m_channel;
        private Server m_server;
        public Playerlistener(Server server) : base("*user.txt")
        {
            m_server = server;
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            if (channelID == null)
            {
                Logger.Error("Got a null channel ID");
                return;
            }
            var channel = client.GetChannel(ulong.Parse(channelID));
            m_channel = channel as IMessageChannel;
            if (m_channel == null)
            {
                Logger.Error($"Channel ID {channelID} does not appear to be valid");
            }
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            string message = "";
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

                var player = m_server.GetOrCreatePlayer(name);
                player.Online = true;
                player.Position = position;
                if (player.LastSeen < line.TimeStamp)
                {
                    player.LastSeen = line.TimeStamp;
                    message = $":wave: {name} has connected";
                }
            }
            else if (line.Message.Contains("disconnected"))
            {
                // The only part of this line in quotes should be the player name, so find that
                var firstQuote = line.Message.IndexOf("\"");
                var lastQuote = line.Message.LastIndexOf("\"");
                var name = line.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                var player = m_server.GetOrCreatePlayer(name);
                player.Online = false;
                if (player.LastSeen < line.TimeStamp)
                {
                    player.LastSeen = line.TimeStamp;
                    message = $":runner: {name} has disconnected";
                }
            }

            if (m_channel == null)
            {
                Logger.Warn("Player notification channel not set");
            }
            else if (message != "")
            {
                await m_channel.SendMessageAsync(message);
                return true;
            }
            return false;
        }
    }
}
