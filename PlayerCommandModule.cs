using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    public class PlayerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly IServiceProvider m_provider;
        public PlayerCommandModule(IServiceProvider provider)
        {
            m_provider = provider;
        }

        [SlashCommand("players", "List the current players")]
        public async Task Players()
        {
            var server = m_provider.GetRequiredService<Server>();
            if (server.players.Count == 0)
            {
                await RespondAsync("No players currently connected", ephemeral: true);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithTitle("Players");

                foreach (var player in server.players)
                {
                    builder.AddField(player.Name, $"Last seen: {player.LastSeen.ToShortDateString()} {player.LastSeen.ToShortTimeString()} at {player.Position.ToString()}");
                }
                await RespondAsync(embed: builder.Build(), ephemeral: true);
            }
        }
    }
}
