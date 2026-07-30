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
            if (m_server.Attach())
            {
                await FollowupAsync("Attached to existing server process");
            }
            else if (await m_server.Start())
            {
                await FollowupAsync("Server started", ephemeral: true);
            }
            else
            {
                await FollowupAsync("Server start failed", ephemeral: true);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("stop", "Stop the server")]
        public async Task Stop()
        {
            await RespondAsync("Stopping server, please wait...", ephemeral: true);
            await m_server.Stop();
            await FollowupAsync("Server Stopped", ephemeral: true);
        }

        private async Task DownloadSteamCMD()
        {
            var downloadFile = "steamcmd.file";
            if (!File.Exists(downloadFile))
            {
                using (var client = new HttpClient())
                {
                    string uri = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd";

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        uri += ".zip";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        uri += "_osx.tar.gz";
                    }
                    else
                    {
                        uri += "_linux.tar.gz";
                    }
                    using (var s = await client.GetStreamAsync(uri))
                    {
                        using (var fs = new FileStream(downloadFile, FileMode.CreateNew))
                        {
                            await s.CopyToAsync(fs);
                        }
                    }
                }
            }

            var unpackedPath = "steamcmd";
            if (!Directory.Exists(unpackedPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ZipFile.ExtractToDirectory(downloadFile, unpackedPath);
                }
                else
                {
                    Directory.CreateDirectory(unpackedPath);
                    using (var fs = new FileStream(downloadFile, FileMode.Open))
                    {
                        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                        {
                            await TarFile.ExtractToDirectoryAsync(gz, unpackedPath, true);
                        }
                    }
                }
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("install", "Install (or update the server from steam")]
        public async Task Install()
        {
            await RespondAsync("Installing server", ephemeral: true);
            await DownloadSteamCMD();

            Process process = new();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.FileName = "steamcmd/steamcmd.sh";
            }
            else
            {
                process.StartInfo.FileName = "steamcmd/steamcmd";
            }
            // Install into the same folder the bot looks in, so a path set in zomboi.ini is honoured
            string[] arguments = {
                "+@ShutdownOnFailedCommand", "1", // Set to 0 if updating multiple servers at once
                "+@NoPromptForPassword", "1",
                "+force_install_dir", m_server.InstallPath,
                "+login", "anonymous",
                "+app_update", "380870", "validate",
                "+quit"
            };
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.Start();
            await process.WaitForExitAsync();

            // Steamcmd reports its own failures by exiting without leaving us anything to run
            var startPath = Server.FindLauncher(m_server.InstallPath);
            if (startPath == null)
            {
                Logger.Error($"Steamcmd finished but there's no launcher in {m_server.InstallPath}");
                await FollowupAsync("Server install failed, check the bot's output for what steam said", ephemeral: true);
                return;
            }

            await FollowupAsync("Server installed", ephemeral: true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We don't want the batch file to wait for input, so remove any PAUSE
                var pause = "PAUSE";
                var file = File.ReadAllText(startPath);
                file = file.Replace(pause, "");
                File.WriteAllText(startPath, file);
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("create", "Create the server")]
        public async Task Create(string password)
        {
            await RespondAsync("Creating server, please wait...", ephemeral: true);
            await m_server.Create(password);
            await FollowupAsync("Server created", ephemeral: true);
        }

        [SlashCommand("status", "Status of the server")]
        public async Task Status()
        {
            var embed = new EmbedBuilder()
                .WithTitle("Server status");

            if (m_server.IsRunning)
            {
                embed.Color = Color.Green;
                var ipString = "";
                try
                {
                    ipString = (await new HttpClient().GetStringAsync("http://icanhazip.com/")).Replace("\\r\\n", "").Replace("\\n", "").Trim();
                }
                catch (Exception e)
                {
                    Logger.Error(e.Message);
                }

                if (!IPAddress.TryParse(ipString, out IPAddress? ipAddress))
                {
                    Logger.Error("Unable to get external IP address");
                }

                var uptimeString = $" {m_server.UpTime.Days}d {m_server.UpTime.Hours}h {m_server.UpTime.Minutes}m {m_server.UpTime.Seconds}s";

                embed.AddField("Up time", uptimeString)
                    .AddField("IP Address", ipAddress == null ? "Unknown" : ipAddress.ToString())
                    .AddField("Port", "16261")
                    .AddField("Player Count", m_server.PlayerCount);
            }
            else
            {
                embed.Color = Color.Red;
                embed.Description = "Server offline";
            }
            await RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}
