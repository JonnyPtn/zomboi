using Discord;
using Discord.WebSocket;

namespace zomboi
{
    public class PerkListener : LogFileListener
    {
        private readonly Server m_server;
        private IMessageChannel? m_channel;

        // The perk log lines are [steam id][name][position][what happened], where the last field is
        // either the full "Perk=level,..." list, the words "Level Changed" followed by [perk][level],
        // or one of the plain events below. Most of them then end with [Hours Survived: n].
        private const int NameField = 1;
        private const int PositionField = 2;
        private const int EventField = 3;
        private const int ChangedPerkField = 4;
        private const int ChangedLevelField = 5;

        // Written as a player's character comes into the world, either at the start of a session or
        // after they've died and made a new one
        private const string LoginEvent = "Login";
        private const string CreatedPlayerEvent = "Created Player";

        public PerkListener(Server server) : base("*PerkLog.txt")
        {
            m_server = server;
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            m_channel = Channels.Find(client, channelID, "skill");
        }

        /// <summary>
        /// Post a level up, if there's a channel to post it to. What the log tells us about a player is
        /// worth keeping either way, so an unset channel skips the notification rather than the line.
        /// </summary>
        private async Task Announce(string playerName, string perkName, int level)
        {
            if (m_channel == null)
            {
                return;
            }
            await m_channel.SendMessageAsync(
                $":chart_with_upwards_trend: {playerName} has achieved level {level} in {perkName}");
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            var fields = LogFields.Bracketed(line.Message);
            if (fields.Count <= EventField)
            {
                return false;
            }
            var name = fields[NameField];
            var happened = fields[EventField];

            // Whatever the line turns out to be about, it says where the player is and usually how long
            // their character has survived, so take both off every one of them
            var player = m_server.PlayerSeen(name, line.TimeStamp,
                LogFields.TryReadCoordinates(fields[PositionField], out var position) ? position : null,
                LogFields.TryReadHoursSurvived(line.Message, out var hours) ? hours : null);

            // A character coming into the world is nothing to post about - the join notification comes
            // from the player log, and beats this line to it - but the survival time above is only in
            // the logs at all because of these lines
            if (happened == LoginEvent || happened.StartsWith(CreatedPlayerEvent, StringComparison.Ordinal))
            {
                return true;
            }

            if (happened.Contains('='))
            {
                var levels = ParsePerkList(happened);
                if (levels.Count == 0)
                {
                    return false;
                }

                // Check against the player's perks to see if they've levelled up
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
                        await Announce(player.Name, perk.Name, perk.Level);
                    }
                }
                return true;
            }

            if (happened.Contains("Level Changed"))
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
                    await Announce(player.Name, perkName, level);
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
