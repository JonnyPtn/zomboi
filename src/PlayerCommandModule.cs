using Discord;
using Discord.Interactions;

namespace zomboi
{
    [Group("player", "Player options")]
    public class PlayerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly Server m_server;
        public PlayerCommandModule(Server server)
        {
            m_server = server;
        }

        [SlashCommand("list", "List the current players")]
        public async Task List()
        {
            var players = m_server.OnlinePlayers;
            if (players.Count == 0)
            {
                await RespondAsync("No players currently connected", ephemeral: true);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithTitle("Players");

                foreach (var player in players)
                {
                    builder.AddField(player.Name, Describe(player));
                }
                await RespondAsync(embed: builder.Build(), ephemeral: true);
            }
        }

        /// <summary>
        /// Where and when we last saw a player, and how long they've lasted, leaving out whichever of
        /// those the logs haven't told us yet.
        /// </summary>
        private static string Describe(Player player)
        {
            var where = player.HasPosition ? $" {Map.Link(player.Position)}" : string.Empty;
            var description = $"Last seen: {player.LastSeen.ToShortDateString()} {player.LastSeen.ToShortTimeString()}{where}";
            if (player.HoursSurvived != null)
            {
                // Only the perk log carries this, so it's as of their last skill event rather than now
                description += $"\nSurvived: {Survival.Describe(player.HoursSurvived.Value)}";
            }
            return description;
        }

        [SlashCommand("skills", "Show skills for a player")]
        public async Task Skills([Summary("Name", "Name of the player to show skills for")] string playerName)
        {
            // Look up rather than create, or a typo here would leave a phantom player in the counts
            var player = m_server.FindPlayer(playerName);
            if (player == null)
            {
                await RespondAsync($"Haven't seen a player called {playerName}", ephemeral: true);
                return;
            }
            if (player.Perks.Count == 0)
            {
                // Discord rejects an embed with an empty description
                await RespondAsync($"No skills recorded for {player.Name} yet", ephemeral: true);
                return;
            }

            var skillString = string.Join("\n", player.Perks.Select(x => x.Name + " : " + x.Level));
            var embed = new EmbedBuilder()
                .WithTitle(player.Name)
                .WithDescription(skillString);
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
