using System.Data.Common;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    [Group("server", "Server options")]
    public class ServerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private const string serverStartID = "server-start";
        private const string serverStopID = "server-stop";
        private readonly Server m_server;

        public ServerCommandModule(IServiceProvider provider)
        {
            m_server = provider.GetRequiredService<Server>();
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("start", "Start the server")]
        public async Task Start() => await m_server.Start();

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("stop", "Stop the server")]
        public async Task Stop() => await m_server.Stop();

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
    