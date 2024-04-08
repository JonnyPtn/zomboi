using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace zomboi
{
    public class PlayerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("players", "List the current players")]
        public async Task Players()
        {
            if (Server.players.Count == 0)
            {
                await RespondAsync("No players currently connected", ephemeral: true);
            }
            else
            {
                var builder = new EmbedBuilder()
                    .WithTitle("Players")
                    .WithDescription(string.Join("\n", Server.players.Select(x => x.Name)));
                await RespondAsync(embed: builder.Build(), ephemeral: true);
            }
        }
    }
}
