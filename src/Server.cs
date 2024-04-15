
using Discord;
using Discord.WebSocket;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zomboi
{
    public class Server
    {
        private static readonly string m_serverPath = "server";
        private static readonly string m_configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Server/servertest.ini");
        public static string StartPath
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
            }
        }
        private Process? m_process;

        public bool IsRunning { get { return m_process != null && !m_process.HasExited; } }
        public TimeSpan UpTime { get { return DateTime.Now - m_startTime; } }
        private bool m_childProcess = false;
        public static bool IsInstalled { get { return Directory.Exists(m_serverPath); } }
        public static bool IsCreated { get { return File.Exists(m_configPath); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Logs")); } }
        private List<Player> m_players = new();
        public List<Player> Players { get { return m_players; } }
        public int PlayerCount { get { return m_players.Count; } }
        private readonly DiscordSocketClient m_client;
        private DateTime m_startTime = DateTime.Now;

        public Server(DiscordSocketClient client)
        {
            m_client = client;
        }

        public delegate void PlayerJoined(Player player);
        public event PlayerJoined? OnPlayerJoined;

        private readonly StreamWriter m_logStream = new (new FileStream("server.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

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
            if (existing.Length > 1)
            {
                var error = $"Found {existing.Count()} existing server processes";
                Logger.Error(error);
            }
            else if (existing.Count() == 1)
            {
                m_process = existing[0];
                m_childProcess = false;
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
                m_childProcess = true;
                m_startTime = DateTime.Now;
                Logger.Info("Server started");
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
            if (m_childProcess && IsRunning && m_process != null)
            {
                await m_process.StandardInput.WriteLineAsync("save");
                await m_process.StandardInput.WriteLineAsync("quit");
                m_process.CancelErrorRead();
                m_process.CancelOutputRead();
                m_logStream.Flush();
                m_process.WaitForExit(TimeSpan.FromSeconds(30));
            }
            if (IsRunning && m_process != null)
            {
                Logger.Info("Unable to stop server cleanly, will be killed");
                m_process.Kill();

            }
            Logger.Info("Server stopped");
            m_childProcess = false;
        }

        public async Task Create(string password)
        {
            if (m_process == null)
            {
                Logger.Error("Process is null");
                return;
            }
            else
            {
                await Start();
                await m_process.StandardInput.WriteLineAsync(password); // Set the password
                await m_process.StandardInput.WriteLineAsync(password); // Confirm the password
                await Stop();
                Logger.Info("Server created");
            }
        }

        public static void AddMod(Int64 modID, string modName)
        {
            if (IsCreated)
            {
                bool addedId = false, addedName = false;
                var lines = File.ReadAllLines(m_configPath);
                foreach (var line in lines.Select((value, i) => new { i, value }))
                {
                    if (line.value.Contains("WorkshopItems"))
                    {
                        if (line.value.Contains(modID.ToString()))
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
                        addedId = true;
                    }
                    else if (line.value.Contains("Mods"))
                    {
                        if (line.value.Contains(modName))
                        {
                            Logger.Warn($"Mod with id {modName} already added");
                            return;
                        }
                        if (line.value.Trim().EndsWith("="))
                        {
                            lines[line.i] = line.value + modName;
                        }
                        else
                        {
                            lines[line.i] = line.value + "," + modName;
                        }
                        addedName = true;
                    }

                    if (addedName && addedId)
                    {
                        File.WriteAllLines(m_configPath, lines);
                        Logger.Info($"Mod added: {modName} ({modID})");
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
