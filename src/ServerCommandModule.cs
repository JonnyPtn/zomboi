using Discord;
using Discord.Interactions;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace zomboi
{
    [Group("zomboi", "Server options")]
    public class ServerCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        // One client for the life of the bot, so we're not leaking a socket per status command
        private static readonly HttpClient m_client = new();
        private const string ExternalIPUri = "http://icanhazip.com/";
        private const string SteamAppID = "380870";

        private readonly Server m_server;
        public ServerCommandModule(Server server)
        {
            m_server = server;
        }
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("start", "Start the server")]
        public async Task Start()
        {
            await RespondAsync("Starting server, please wait", ephemeral: true);
            if (await m_server.Attach())
            {
                await FollowupAsync("Attached to existing server process", ephemeral: true);
            }
            else if (await m_server.Start())
            {
                await FollowupAsync("Server started", ephemeral: true);
            }
            else
            {
                await FollowupAsync("Server start failed, check the bot's output for why", ephemeral: true);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("stop", "Stop the server")]
        public async Task Stop()
        {
            await RespondAsync("Stopping server, please wait...", ephemeral: true);
            if (await m_server.Stop())
            {
                await FollowupAsync("Server stopped", ephemeral: true);
            }
            else
            {
                await FollowupAsync("The server wasn't running", ephemeral: true);
            }
        }

        /// <summary>
        /// The steamcmd package for this platform, and the executable inside it once unpacked.
        /// </summary>
        private static (string Uri, string Executable) SteamCMD
        {
            get
            {
                const string uri = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd";
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return (uri + ".zip", Path.Combine("steamcmd", "steamcmd.exe"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // The mac package ships the same shell script as the linux one, not a binary
                    return (uri + "_osx.tar.gz", Path.Combine("steamcmd", "steamcmd.sh"));
                }
                else
                {
                    return (uri + "_linux.tar.gz", Path.Combine("steamcmd", "steamcmd.sh"));
                }
            }
        }

        private static async Task DownloadSteamCMD()
        {
            var unpackedPath = "steamcmd";
            if (Directory.Exists(unpackedPath))
            {
                return;
            }

            // Always fetch a fresh copy - a half finished download from an earlier attempt would
            // otherwise be unpacked over and over, failing the same way each time
            var downloadFile = "steamcmd.download";
            using (var stream = await m_client.GetStreamAsync(SteamCMD.Uri))
            using (var file = new FileStream(downloadFile, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(file);
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ZipFile.ExtractToDirectory(downloadFile, unpackedPath);
                }
                else
                {
                    Directory.CreateDirectory(unpackedPath);
                    using var file = new FileStream(downloadFile, FileMode.Open, FileAccess.Read);
                    using var gz = new GZipStream(file, CompressionMode.Decompress);
                    await TarFile.ExtractToDirectoryAsync(gz, unpackedPath, true);
                }
            }
            catch
            {
                // A partly unpacked folder would make the next run skip the download and use it
                try
                {
                    Directory.Delete(unpackedPath, recursive: true);
                }
                catch (Exception cleanup)
                {
                    Logger.Warn($"Couldn't clean up {unpackedPath}: {cleanup.Message}");
                }
                throw;
            }
            finally
            {
                File.Delete(downloadFile);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("install", "Install (or update) the server from steam")]
        public async Task Install()
        {
            await RespondAsync("Installing server, this can take a while", ephemeral: true);
            try
            {
                await DownloadSteamCMD();
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to download steamcmd: {exception.Message}");
                await Report("Couldn't download steamcmd, check the bot's output for why");
                return;
            }

            Process process = new();
            process.StartInfo.FileName = SteamCMD.Executable;
            // Install into the same folder the bot looks in, so a path set in zomboi.ini is honoured
            string[] arguments = {
                "+@ShutdownOnFailedCommand", "1", // Set to 0 if updating multiple servers at once
                "+@NoPromptForPassword", "1",
                "+force_install_dir", m_server.InstallPath,
                "+login", "anonymous",
                "+app_update", SteamAppID, "validate",
                "+quit"
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            try
            {
                process.Start();
                await process.WaitForExitAsync();
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to run {SteamCMD.Executable}: {exception.Message}");
                await Report("Couldn't run steamcmd, check the bot's output for why");
                return;
            }

            // Steamcmd reports its own failures by exiting without leaving us anything to run
            var startPath = Server.FindLauncher(m_server.InstallPath);
            if (startPath == null)
            {
                Logger.Error($"Steamcmd finished but there's no launcher in {m_server.InstallPath}");
                await Report("Server install failed, check the bot's output for what steam said");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We don't want the batch file to wait for input, so remove any PAUSE
                var file = File.ReadAllText(startPath);
                File.WriteAllText(startPath, file.Replace("PAUSE", ""));
            }

            await Report("Server installed");
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("create", "Create the server")]
        public async Task Create(string password)
        {
            await RespondAsync("Creating server, this can take several minutes...", ephemeral: true);
            var created = await m_server.Create(password);
            await Report(created
                ? "Server created"
                : "Server creation failed, check the bot's output for why");
        }

        [SlashCommand("status", "Status of the server")]
        public async Task Status()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Server status");

            if (m_server.IsRunning)
            {
                embed.Color = Color.Green;
                var address = await ExternalAddress();

                var uptimeString = $" {m_server.UpTime.Days}d {m_server.UpTime.Hours}h {m_server.UpTime.Minutes}m {m_server.UpTime.Seconds}s";

                embed.AddField("Up time", uptimeString)
                    .AddField("IP Address", address == null ? "Unknown" : address.ToString())
                    .AddField("Port", m_server.Port)
                    .AddField("Player Count", m_server.PlayerCount);
            }
            else
            {
                embed.Color = Color.Red;
                embed.Description = "Server offline";
            }
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        /// <summary>
        /// The address players outside the network need to connect on, or null if we can't find it out.
        /// </summary>
        private static async Task<IPAddress?> ExternalAddress()
        {
            string response;
            try
            {
                response = await m_client.GetStringAsync(ExternalIPUri);
            }
            catch (Exception exception)
            {
                Logger.Error($"Unable to get external IP address: {exception.Message}");
                return null;
            }

            if (!IPAddress.TryParse(response.Trim(), out var address))
            {
                Logger.Error($"'{response.Trim()}' isn't an IP address, which is what {ExternalIPUri} was meant to give us");
                return null;
            }
            return address;
        }

        /// <summary>
        /// Follow up on a command that took a while. Discord stops accepting followups 15 minutes after
        /// the interaction, and an install can outlast that.
        /// </summary>
        private async Task Report(string message)
        {
            try
            {
                await FollowupAsync(message, ephemeral: true);
            }
            catch (Exception exception)
            {
                Logger.Info($"Result was '{message}'");
                DiscordError.Log("report the result back to discord", exception);
            }
        }
    }
}
