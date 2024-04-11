﻿using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
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
        private readonly Server m_server;
        public ServerCommandModule(Server server)
        {
            m_server = server;
        }
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("start", "Start the server")]
        public async Task Start()
        {
            await RespondAsync("Starting server, please wait");
            if (await m_server.Start())
            {
                await FollowupAsync("Server started");
            }
            else
            {
                await FollowupAsync("Server start failed");
            }
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("stop", "Stop the server")]
        public async Task Stop()
        {
            await RespondAsync("Stopping server, please wait...");
            await m_server.Stop();
            await FollowupAsync("Server Stopped");
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
            await RespondAsync("Installing server");
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
            process.StartInfo.Arguments = "+runscript ../update_server.txt";
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.Start();
            await process.WaitForExitAsync();
            await FollowupAsync("Server installed");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We don't want the batch file to wait for input, so remove any PAUSE
                var pause = "PAUSE";
                var file = File.ReadAllText(Server.StartPath);
                file = file.Replace(pause, "");
                File.WriteAllText(Server.StartPath, file);
            }
        }
        
        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("create", "Create the server")]        
        public async Task Create(string password)
        {
            await RespondAsync("Creating server, please wait...");
            await m_server.Create(password);
            await FollowupAsync("Server created");
        }

    }
}
