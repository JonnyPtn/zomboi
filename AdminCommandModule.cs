using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace zomboi
{
    public class AdminCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private const string serverStartID = "server-start";
        private const string serverStopID = "server-stop";
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

        public static async Task ServerButtonHandler(SocketMessageComponent component)
        {
            switch (component.Data.CustomId)
            {
                case serverStartID:
                    await component.RespondAsync("Starting server", ephemeral: true);
                    Server.Start();
                    break;
                case serverStopID:
                    await component.RespondAsync("Stopping server", ephemeral: true);
                    await Server.Stop();
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
                await Server.Create(password);
                await FollowupAsync("Server created succesfully", ephemeral: true);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("install", "Install (or update the server from steam")]
        public async Task Install()
        {
            await RespondAsync("Installing Server, please wait...", ephemeral: true);
            await Server.Install();
            await FollowupAsync("Server Installed", ephemeral: true);
        }
    }
}
    