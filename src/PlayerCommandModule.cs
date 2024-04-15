using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    [Group("player", "Player options")]
    public class PlayerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IServiceProvider m_provider;
        public PlayerCommandModule(IServiceProvider provider)
        {
            m_provider = provider;
        }

        [SlashCommand("list", "List the current players")]
        public async Task List()
        {
            var server = m_provider.GetRequiredService<Server>();
            if (server.PlayerCount == 0)
            {
                await RespondAsync("No players currently connected", ephemeral: true);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithTitle("Players");

                foreach (var player in server.Players)
                {
                    builder.AddField(player.Name, $"Last seen: {player.LastSeen.ToShortDateString()} {player.LastSeen.ToShortTimeString()} at {player.Position}");
                }
                await RespondAsync(embed: builder.Build(), ephemeral: true);
            }
        }

        [SlashCommand("skills", "Show skills for a player")]
        public async Task Skills([Summary("Name", "Name of the player to show skills for")] string playerName)
        {
            var player = m_provider.GetRequiredService<Server>().GetOrCreatePlayer(playerName, DateTime.UnixEpoch);
            if (player.Perks.Count == 0)
            {
                Logger.Warn("Player perks are empty");
            }
            var skillString = string.Join("\n", player.Perks.Select(x => x.Name + " : " + x.Level));
            var embed = new EmbedBuilder()
                .WithTitle(player.Name)
                .WithDescription(skillString);
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
