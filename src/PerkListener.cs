using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    public class PerkListener : LogFileListener
    {
        private readonly IServiceProvider m_provider;
        private IMessageChannel? m_channel;

        public PerkListener(IServiceProvider provider) : base("*PerkLog.txt")
        {
            m_provider = provider;
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

                openBracket = line.Message.IndexOf("[", closeBracket);
                closeBracket = line.Message.IndexOf("]", openBracket);
                var name = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);

                var player = m_provider.GetRequiredService<Server>().GetOrCreatePlayer(name, line.TimeStamp);

                openBracket = line.Message.IndexOf("[", closeBracket);
                closeBracket = line.Message.IndexOf("]", openBracket);
                var position = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);

                openBracket = line.Message.IndexOf("[", closeBracket);
                closeBracket = line.Message.IndexOf("]", openBracket);
                var perks = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);

                if (perks.Contains('='))
                {
                    // Should be a list of key=value perks?!
                    var perkPairs = perks.Split(",", StringSplitOptions.TrimEntries);
                    var perkValues = perkPairs.Select(x =>
                    {
                        var split = x.Split("=");
                        return new Perk(split[0], int.Parse(split[1]));
                    }).ToArray();

                    // Check against the player's perks to see if they've levelled up
                    foreach (var perk in perkValues)
                    {
                        var existing = player.Perks.Find(x => x.Name == perk.Name);
                        if (existing == null)
                        {
                            player.Perks.Add(perk);
                        }
                        else if (perk.Level > existing.Level)
                        {
                            await m_channel.SendMessageAsync($":chart_with_upwards_trend: {player.Name} has achieved level {perk.Level} in {perk.Name}");
                            existing.Level = perk.Level;
                        }
                    }
                    return true;
                }
                else if (perks.Contains("Level Changed"))
                {
                    openBracket = line.Message.IndexOf("[", closeBracket);
                    closeBracket = line.Message.IndexOf("]", openBracket);
                    var perkName = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);

                    openBracket = line.Message.IndexOf("[", closeBracket);
                    closeBracket = line.Message.IndexOf("]", openBracket);
                    var level = line.Message.Substring(openBracket + 1, closeBracket - openBracket - 1);

                    var current = player.Perks.Find(x => x.Name == perkName);
                    if (current == null)
                    {
                        Logger.Warn($"Unexpected skill for player: {perkName}");
                    }
                    else
                    {
                        await m_channel.SendMessageAsync($":chart_with_upwards_trend: {player.Name} has achieved level {level} in {perkName}");
                        current.Level = int.Parse(level);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
