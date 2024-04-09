using System.Data.Common;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    public class AdminCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private const string serverStartID = "server-start";
        private const string serverStopID = "server-stop";
        private readonly IServiceProvider m_provider;
        private readonly Server m_server;

        public AdminCommandModule(IServiceProvider provider)
        {
            m_provider = provider;
            m_server =  provider.GetRequiredService<Server>();
            provider.GetRequiredService<DiscordSocketClient>().ButtonExecuted += ServerButtonHandler;
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("server", "Server management options")]
        public async Task ManageServer()
        {
            if (!Server.IsInstalled)
            {
                await RespondAsync("Server not installed, use /install command", ephemeral: true);
                return;
            }
            else if (!Server.IsCreated)
            {
                await RespondAsync("Server not created, use /create command", ephemeral: true);
                return;
            }

            var builder = new ComponentBuilder()
                 .WithButton("Start", serverStartID, style: ButtonStyle.Success)
                 .WithButton("Stop", serverStopID, style: ButtonStyle.Danger);

            await RespondAsync("Server options:", components: builder.Build(), ephemeral: true);
        }

        public async Task ServerButtonHandler(SocketMessageComponent component)
        {
            switch (component.Data.CustomId)
            {
                case serverStartID:
                    await component.RespondAsync("Starting server", ephemeral: true);
                    await m_server.Start();
                    break;
                case serverStopID:
                    await component.RespondAsync("Stopping server", ephemeral: true);
                    await m_server.Stop();
                    break;
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("create", "Create the server")]
        public async Task Create([Summary("password", "Admin password for new server")] string password)
        {
            if (Server.IsCreated)
            {
                await RespondAsync("Server already created", ephemeral: true);
            }
            else
            {
                await RespondAsync("Creating server, please wait...", ephemeral: true);
                await m_server.Create(password);
                await FollowupAsync("Server created succesfully", ephemeral: true);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("install", "Install (or update the server from steam")]
        public async Task Install()
        {
            await RespondAsync("Installing Server, please wait...", ephemeral: true);
            await m_server.Install();
            await FollowupAsync("Server Installed", ephemeral: true);
        }
    }
}
    