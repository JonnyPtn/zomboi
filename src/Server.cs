
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
        private static string m_serverPath = "server";
        private static string m_configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Server/servertest.ini");
        public static string StartPath { get 
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{m_serverPath}/StartServer64.bat";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{m_serverPath}/StartServer.Command";
            }
            else
            {
                return $"{m_serverPath}/start-server.sh";
            }
        }}
        private Process? m_process;

        public bool IsRunning { get { return m_process != null && !m_process.HasExited; } }
        public TimeSpan UpTime { get { return DateTime.Now - m_startTime;} }
        public bool IsChildProcess { get; private set; }
        public static bool IsInstalled { get { return Directory.Exists(m_serverPath); } }
        public static bool IsCreated { get { return File.Exists(m_configPath); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Logs")); } }
        private List<Player> m_players = new();
        public List<Player> Players { get { return m_players;}}
        public int PlayerCount { get { return m_players.Count;}}
        private readonly DiscordSocketClient m_client;
        private DateTime m_startTime = DateTime.Now;

        public Server(DiscordSocketClient client)
        {
            m_client = client;
        }

        public delegate void PlayerJoined(Player player);
        public event PlayerJoined? OnPlayerJoined;

        private readonly StreamWriter m_logStream = new StreamWriter(new FileStream("server.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        public Player GetOrCreatePlayer(string playerName, DateTime seenTime)
        {
            var player = m_players.Find(x => x.Name == playerName);
            if (player == null)
            {
                Logger.Info($"Adding Player {playerName}");
                player = new Player(playerName, seenTime, new Vector2(), new List<Perk>());
                m_players.Add(player);
            }

            // if it was before the server started then don't send an event
            if (seenTime > m_startTime && !player.Online)
            {
                if (OnPlayerJoined != null)
                {
                    OnPlayerJoined.Invoke(m_players.Last());
                }
            }

            return player;
        }

        public bool Attach()
        {
            // On windows I'm not yet sure what process name will find the server
            // As "ProjectZomboid64" is the actual game client so causes issues
            // when debugging with a local server which I currently only do on windows
            // So for now just don't try to attach on windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Logger.Info("Windows attach not implemented");
                return false;
            }
            var existing = Process.GetProcessesByName("ProjectZomboid64");
            if (existing.Count() > 1)
            {
                var error = $"Found {existing.Count()} existing server processes";
                Logger.Error(error);
            }
            else if (existing.Count() == 1)
            {
                m_process = existing[0];
                IsChildProcess = false;
                m_startTime = DateTime.Now;
                Logger.Info("Attached to server process");
                return true;
            }
            return false;
        }
        
        public async Task<bool> Start()
        {
            if (IsRunning)
            {
                Logger.Warn("Trying to start server that's already running");
                return false;
            }

            m_process = new();
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
                IsChildProcess = true;
                m_startTime = DateTime.Now;
            }
            else
            {
                var error = $"Failed to start server {m_process.StandardError}";
                Logger.Error(error);
                return false;
            }
            await m_client.SetGameAsync("Project Zomboid");
            await m_client.SetStatusAsync(UserStatus.Online);
            return true;
        }

        public async Task Stop()
        {
            await m_client.SetGameAsync(null);
            await m_client.SetStatusAsync(UserStatus.DoNotDisturb);
            if (IsChildProcess && IsRunning)
            {
                await m_process.StandardInput.WriteLineAsync("save");
                await m_process.StandardInput.WriteLineAsync("quit");
                m_process.CancelErrorRead();
                m_process.CancelOutputRead();
                m_logStream.Flush();
                m_process.WaitForExit(TimeSpan.FromSeconds(30));
            }
            if (IsRunning)
            {
                Logger.Info("Unable to stop server cleanly, will be killed");
                m_process.Kill();

            }
            IsChildProcess = false;
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
        public async Task Create(string password)
        {
            await Start();
            await m_process.StandardInput.WriteLineAsync(password); // Set the password
            await m_process.StandardInput.WriteLineAsync(password); // Confirm the password
            await Stop();
        }

        public static void AddMod(string modID)
        {
            if (IsCreated)
            {
                var lines = File.ReadAllLines(m_configPath);
                foreach (var line in lines.Select((value, i) => new { i, value }))
                {
                    if (line.value.Contains("WorkshopItems"))
                    {
                        if (line.value.Contains(modID))
                        {
                            Logger.Warn($"Mod with id {modID} already added");
                            return;
                        }
                        if (line.value.Trim().EndsWith("="))
                        {
                            lines[line.i] = line.value + modID;
                        }
                        else
                        {
                            lines[line.i] = line.value + "," + modID;
                        }
                        File.WriteAllLines(m_configPath, lines);
                        return;
                    }
                }

            }
            else
            {
                Logger.Error("Can't add mod before server is created");
            }
        }
    }
}
