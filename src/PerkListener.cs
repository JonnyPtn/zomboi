
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Discord;
using Discord.WebSocket;

namespace zomboi
{
    public class PerkListener : LogFileListener
    {
        private readonly Server m_server;
        private IMessageChannel? m_channel;

        public PerkListener(Server server) : base("*PerkLog.txt")
        {
            m_server = server;
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            if (channelID == null || channelID == "")
            {
                Logger.Error($"Bad channel ID: {channelID}");
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
            if (m_channel != null)
            {
                // Deconstruct the message based on the brackets
                var closeBracket = line.Message.IndexOf("]");
                var openBracket = line.Message.IndexOf("[");
                var someNumber = line.Message.Substring(openBracket + 1, closeBracket - 1);
                Logger.Info($"Perk Some Number:{someNumber}");

                openBracket = line.Message.IndexOf("[", closeBracket);
                closeBracket = line.Message.IndexOf("]", openBracket);
                var name = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);
                Logger.Info($"Perk Player Name:{name}");

                openBracket = line.Message.IndexOf("[", closeBracket);
                closeBracket = line.Message.IndexOf("]", openBracket);
                var perks = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);
                Logger.Info($"Perks: {perks}");

                if (perks.Contains("="))
                {
                    // Should be a list of key=value perks?!
                    var perkPairs = perks.Split(",", StringSplitOptions.TrimEntries);
                    var perkValues = perkPairs.Select(x => 
                    {
                        var split = x.Split("=");
                        return new Perk(split[0],int.Parse(split[1]));
                    }).ToArray();

                    var player = m_server.GetOrCreatePlayer(name);
                    
                    // Check against the player's perks to see if they've levelled up
                    foreach(var perk in perkValues)
                    {
                        var existing = player.Perks.Find(x => x.Name == perk.Name);
                        if (existing != null && perk.Level > existing.Level)
                        {
                            await m_channel.SendMessageAsync($":chart_with_upwards_trend: {player.Name} has achieved level {perk.Level} in {perk.Name}");
                            existing.Level = perk.Level;
                        }
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
