using Discord;
using Discord.Interactions;

namespace zomboi
{
    public class AdminCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("start", "Start the server")]
        public async Task Start()
        {
            if (!Server.IsInstalled)
            {
                await RespondAsync("Server not installed, use /install command", ephemeral: true);
            }
            else if (!Server.IsCreated)
            {
                await RespondAsync("Server not created, use /create command", ephemeral: true);
            }
            else if (Server.IsRunning)
            {
                await RespondAsync("Server already running, did you mean /restart?", ephemeral: true);
            }
            else
            {
                if (Server.Start())
                {
                    await RespondAsync("Server started", ephemeral: true);
                }
                else
                {
                    await RespondAsync("Failed to start server", ephemeral: true);
                }
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("stop", "Stop the server")]
        public async Task Stop()
        {
            if (!Server.IsInstalled)
            {
                await RespondAsync("Server not installed, use /install command", ephemeral: true);
            }
            else if (!Server.IsCreated)
            {
                await RespondAsync("Server not created, use /create command", ephemeral: true);
            }
            else if (!Server.IsRunning)
            {
                await RespondAsync("Server isn't running", ephemeral: true);
            }
            else
            {
               await Server.Stop();
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
            await RespondAsync("Installing Server, please wait...");
            await Server.Install();
            await FollowupAsync("Server Installed");
        }
    }
}
    