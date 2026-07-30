using Discord;
using Discord.WebSocket;

namespace zomboi
{
    public class PerkListener : LogFileListener
    {
        private readonly Server m_server;
        private IMessageChannel? m_channel;

        // The perk log lines are [number][name][position][perks], where perks is either the full
        // "Perk=level,..." list or the words "Level Changed" followed by [perk][level]
        private const int NameField = 1;
        private const int PerksField = 3;
        private const int ChangedPerkField = 4;
        private const int ChangedLevelField = 5;

        public PerkListener(Server server) : base("*PerkLog.txt")
        {
            m_server = server;
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            m_channel = FindChannel(client, channelID, "skill");
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            if (m_channel == null)
            {
                return false;
            }

            var fields = LogFields.Bracketed(line.Message);
            if (fields.Count <= PerksField)
            {
                return false;
            }
            var name = fields[NameField];
            var perks = fields[PerksField];

            if (perks.Contains('='))
            {
                var levels = ParsePerkList(perks);
                if (levels.Count == 0)
                {
                    return false;
                }

                // Check against the player's perks to see if they've levelled up
                var player = m_server.GetOrCreatePlayer(name, line.TimeStamp);
                foreach (var perk in levels)
                {
                    var existing = player.Perks.Find(x => x.Name == perk.Name);
                    if (existing == null)
                    {
                        player.Perks.Add(perk);
                    }
                    else if (perk.Level > existing.Level)
                    {
                        existing.Level = perk.Level;
                        await m_channel.SendMessageAsync(
                            $":chart_with_upwards_trend: {player.Name} has achieved level {perk.Level} in {perk.Name}");
                    }
                }
                return true;
            }

            if (perks.Contains("Level Changed"))
            {
                if (fields.Count <= ChangedLevelField)
                {
                    Logger.Warn($"Level change with no skill and level in it: {line.Message}");
                    return false;
                }
                var perkName = fields[ChangedPerkField];
                if (!int.TryParse(fields[ChangedLevelField], out var level))
                {
                    Logger.Warn($"'{fields[ChangedLevelField]}' is not a level for {perkName}");
                    return false;
                }

                var player = m_server.GetOrCreatePlayer(name, line.TimeStamp);
                var current = player.Perks.Find(x => x.Name == perkName);
                if (current == null)
                {
                    // No full skill list yet, so we can't tell a level up from a level we hadn't recorded
                    Logger.Warn($"Unexpected skill for player: {perkName}");
                    player.Perks.Add(new Perk(perkName, level));
                }
                else if (level > current.Level)
                {
                    current.Level = level;
                    await m_channel.SendMessageAsync(
                        $":chart_with_upwards_trend: {player.Name} has achieved level {level} in {perkName}");
                }
                else
                {
                    current.Level = level;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// A "Perk=level,Perk=level" list, skipping any entry that isn't in that shape.
        /// </summary>
        private static List<Perk> ParsePerkList(string perks)
        {
            var levels = new List<Perk>();
            foreach (var pair in perks.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var split = pair.Split('=');
                if (split.Length != 2 || !int.TryParse(split[1], out var level))
                {
                    Logger.Warn($"Unexpected perk entry: {pair}");
                    continue;
                }
                levels.Add(new Perk(split[0].Trim(), level));
            }
            return levels;
        }
    }
}
