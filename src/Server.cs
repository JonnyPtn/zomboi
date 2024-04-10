using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Numerics;
using System.Runtime.InteropServices;
using Discord;
using Discord.WebSocket;

namespace zomboi
{
    public class Server
    {
        private static string serverPath = "server";
        private static string StartPath { get 
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{serverPath}/StartServer64.bat";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{serverPath}/StartServer.Command";
            }
            else
            {
                return $"{serverPath}/start-server.sh";
            }
        }}
        private readonly Process m_process = new();

        public bool IsRunning { get { return m_process.StartInfo.FileName.Length > 0 && !m_process.HasExited; } }
        public static bool IsInstalled { get { return Directory.Exists(serverPath); } }
        public static bool IsCreated { get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Server/servertest.ini")); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Logs")); } }
        private List<Player> m_players = new List<Player>();
        public List<Player> Players { get { return m_players;}}
        public int PlayerCount { get { return m_players.Count;}}
        private readonly DiscordSocketClient m_client;
        public Server(DiscordSocketClient client)
        {
            m_client = client;
        }

        private readonly StreamWriter m_logStream = new StreamWriter(new FileStream("logs/server.log", FileMode.Create));

        public Player GetOrCreatePlayer(string playerName)
        {
            var existing = m_players.Find(x => x.Name == playerName);
            if (existing == null)
            {
                m_players.Add(new Player(playerName, DateTime.Now, new Vector2(), new List<Perk>()));
            }
            return existing??m_players.Find(x => x.Name == playerName)??m_players.Last();
        }
        
        public async Task<bool> Start()
        {
            await m_client.SetActivityAsync(new Game("Starting", ActivityType.Listening, ActivityProperties.Embedded));
            m_process.StartInfo.FileName = StartPath;
            m_process.StartInfo.RedirectStandardInput = true;
            m_process.StartInfo.RedirectStandardOutput = true;
            m_process.StartInfo.RedirectStandardError = true;
            m_process.OutputDataReceived += (sender, e) => m_logStream.WriteLine(e.Data);
            m_process.ErrorDataReceived += (sender, e) => m_logStream.WriteLine(e.Data);
            if (m_process.Start())
            {
                m_process.BeginErrorReadLine();
                m_process.BeginOutputReadLine();
                await m_client.SetActivityAsync(new Game("Project Zomboid", ActivityType.Playing, ActivityProperties.None));
                await m_client.SetStatusAsync(UserStatus.Online);
            }
            else
            {
                Logger.Error($"Failed to start server {m_process.StandardError}");
            }
            return IsRunning;
        }

        public async Task Stop()
        {
            await m_client.SetActivityAsync(new Game("Stopping", ActivityType.Watching, ActivityProperties.Spectate));
            await m_process.StandardInput.WriteLineAsync("save");
            await m_process.StandardInput.WriteLineAsync("quit");
            await m_process.WaitForExitAsync();
            await m_client.SetStatusAsync(UserStatus.AFK);
            await m_client.SetActivityAsync(null);
        }

        private async Task DownloadSteamCMD()
        {
            await m_client.SetActivityAsync(new Game("updating steam", ActivityType.Streaming, ActivityProperties.Sync));
            await m_client.SetStatusAsync(UserStatus.DoNotDisturb);
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

        public async Task Install()
        {
            await DownloadSteamCMD();

            await m_client.SetActivityAsync(new Game("updating game", ActivityType.Streaming, ActivityProperties.Sync));
            await m_client.SetStatusAsync(UserStatus.DoNotDisturb);

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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We don't want the batch file to wait for input, so remove any PAUSE
                var pause = "PAUSE";
                var file = File.ReadAllText(StartPath);
                file = file.Replace(pause, "");
                File.WriteAllText(StartPath, file);
            }
        }

        public async Task Create(string password)
        {
            await Start();
            await m_process.StandardInput.WriteLineAsync(password); // Set the password
            await m_process.StandardInput.WriteLineAsync(password); // Confirm the password
            await Stop();
        }

    }
}
