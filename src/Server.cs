
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace zomboi
{
    public class Server
    {
        // The install layout the server ships with, as described at https://pzwiki.net/wiki/Dedicated_server
        private const string SteamAppFolder = "Project Zomboid Dedicated Server";
        private const string LocalFolder = "server";
        // The user data folder is the same on every platform, it's only the install that moves around
        private static readonly string m_userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");
        private static readonly string m_configPath = Path.Combine(m_userDataPath, "Server/servertest.ini");

        /// <summary>
        /// The launcher scripts the server ships with for this platform, most preferred first.
        /// </summary>
        private static string[] LauncherNames
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return new[] { "StartServer64.bat", "StartServer64_nosteam.bat", "StartServer32.bat" };
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return new[] { "StartServer.Command" };
                }
                else
                {
                    return new[] { "start-server.sh" };
                }
            }
        }

        /// <summary>
        /// Where the steam client puts the server if you install it through the library.
        /// </summary>
        private static IEnumerable<string> SteamInstallPaths
        {
            get
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Steam itself is 32 bit, so on 64 bit windows it installs under Program Files (x86)
                    yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", SteamAppFolder);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    yield return Path.Combine(home, "Library", "Application Support", "Steam", "steamapps", "common", SteamAppFolder);
                }
                else
                {
                    // The first of these is normally a link to the second, but that's not guaranteed
                    yield return Path.Combine(home, ".steam", "steam", "steamapps", "common", SteamAppFolder);
                    yield return Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", SteamAppFolder);
                }
            }
        }

        /// <summary>
        /// The launcher inside a specific folder, or null if that folder doesn't hold an install.
        /// </summary>
        public static string? FindLauncher(string folder)
        {
            return LauncherNames.Select(name => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
        }

        private Process? m_process;

        public bool IsRunning { get { return m_process != null && !m_process.HasExited; } }
        public TimeSpan UpTime { get { return DateTime.Now - m_startTime; } }
        private bool m_childProcess = false;

        /// <summary>
        /// Where the install command puts the server: wherever zomboi.ini points it, or a folder
        /// next to the bot if it doesn't point anywhere.
        /// </summary>
        public string InstallPath
        {
            get
            {
                var configured = m_configuration["server:path"];
                return Path.GetFullPath(string.IsNullOrWhiteSpace(configured) ? LocalFolder : configured.Trim());
            }
        }

        /// <summary>
        /// Every folder we look for an install in, most preferred first. Steam's own locations are
        /// included so a server installed through the steam client is found without any config.
        /// </summary>
        public IEnumerable<string> SearchPaths
        {
            get
            {
                yield return InstallPath;
                foreach (var path in SteamInstallPaths)
                {
                    yield return path;
                }
            }
        }

        /// <summary>
        /// The launcher of the first install we can find, or null if there isn't one anywhere.
        /// </summary>
        public string? StartPath { get { return SearchPaths.Select(FindLauncher).FirstOrDefault(path => path != null); } }

        public bool IsInstalled { get { return StartPath != null; } }
        public static bool IsCreated { get { return File.Exists(m_configPath); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(m_userDataPath, "Logs")); } }
        private readonly List<Player> m_players = new();
        // The log listeners add to this from thread pool threads while the commands read it
        private readonly object m_playersLock = new();

        /// <summary>
        /// A snapshot of every player we've seen this run, safe to enumerate while the listeners work.
        /// </summary>
        public List<Player> Players { get { lock (m_playersLock) { return new List<Player>(m_players); } } }
        public List<Player> OnlinePlayers { get { lock (m_playersLock) { return m_players.FindAll(player => player.Online); } } }
        public int PlayerCount { get { lock (m_playersLock) { return m_players.Count(player => player.Online); } } }

        private readonly DiscordSocketClient m_client;
        private readonly IConfiguration m_configuration;
        private DateTime m_startTime = DateTime.Now;

        public Server(DiscordSocketClient client, IConfiguration configuration)
        {
            m_client = client;
            m_configuration = configuration;
        }

        public delegate Task PlayerJoined(Player player);
        public event PlayerJoined? OnPlayerJoined;

        private readonly StreamWriter m_logStream = new (new FileStream("server.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite));

        /// <summary>
        /// The player we know by that name, or null if we haven't seen them this run.
        /// </summary>
        public Player? FindPlayer(string playerName)
        {
            lock (m_playersLock)
            {
                return m_players.Find(x => string.Equals(x.Name, playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>
        /// The player we know by that name, adding them if this is the first we've heard of them. This
        /// isn't the same as them connecting, which only the player log can tell us about.
        /// </summary>
        public Player GetOrCreatePlayer(string playerName, DateTime seenTime)
        {
            lock (m_playersLock)
            {
                var existing = m_players.Find(x => x.Name == playerName);
                if (existing != null)
                {
                    return existing;
                }
                Logger.Info($"Adding Player {playerName}");
                var player = new Player(playerName, seenTime, new Vector2(), new List<Perk>());
                m_players.Add(player);
                return player;
            }
        }

        /// <summary>
        /// Record a player connecting, telling anyone listening if they weren't already online.
        /// </summary>
        public async Task<Player> PlayerConnected(string playerName, DateTime seenTime, Vector2? position)
        {
            var player = GetOrCreatePlayer(playerName, seenTime);
            bool joined;
            lock (m_playersLock)
            {
                // If it was before the server started then it's history, not someone joining now
                joined = !player.Online && seenTime > m_startTime;
                player.Online = true;
                if (position != null)
                {
                    player.Position = position.Value;
                }
                if (player.LastSeen < seenTime)
                {
                    player.LastSeen = seenTime;
                }
            }

            if (joined && OnPlayerJoined != null)
            {
                // Invoke the handlers by hand so each one is awaited, rather than just the last
                foreach (PlayerJoined handler in OnPlayerJoined.GetInvocationList())
                {
                    await handler(player);
                }
            }
            return player;
        }

        /// <summary>
        /// Record a player disconnecting.
        /// </summary>
        /// <returns>Whether they were online, so we don't announce the same player leaving twice</returns>
        public bool PlayerDisconnected(string playerName, DateTime seenTime)
        {
            var player = GetOrCreatePlayer(playerName, seenTime);
            lock (m_playersLock)
            {
                if (player.LastSeen > seenTime)
                {
                    return false;
                }
                player.LastSeen = seenTime;
                if (!player.Online)
                {
                    return false;
                }
                player.Online = false;
                return true;
            }
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

            var startPath = StartPath;
            if (startPath == null)
            {
                var halfInstalled = SearchPaths.FirstOrDefault(Directory.Exists);
                if (halfInstalled != null)
                {
                    Logger.Error($"{halfInstalled} has no launcher in it ({string.Join(" or ", LauncherNames)}), " +
                        "so the install didn't finish. Run /zomboi install again.");
                }
                else
                {
                    Logger.Error($"No server found. Looked in: {string.Join(", ", SearchPaths)}. " +
                        "Use the /zomboi install command to download it from steam and /zomboi create to set it " +
                        "up, or set 'path' in the [server] section of zomboi.ini if it's already installed elsewhere.");
                }
                return false;
            }

            m_process = new();
            m_process.StartInfo.FileName = startPath;
            // The launchers use paths relative to the server folder. StartServer64.bat cd's itself
            // but the others don't all bother, so start them somewhere that works either way
            m_process.StartInfo.WorkingDirectory = Path.GetDirectoryName(startPath) ?? Environment.CurrentDirectory;
            m_process.StartInfo.RedirectStandardInput = true;
            m_process.StartInfo.RedirectStandardOutput = true;
            m_process.StartInfo.RedirectStandardError = true;
            m_process.OutputDataReceived += (sender, e) => m_logStream.WriteLine(e.Data);
            m_process.ErrorDataReceived += (sender, e) => m_logStream.WriteLine(e.Data);
            try
            {
                if (!m_process.Start())
                {
                    Logger.Error($"Failed to start {startPath}, no new process was created");
                    return false;
                }
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to start {startPath}: {exception.Message}");
                return false;
            }
            m_process.BeginErrorReadLine();
            m_process.BeginOutputReadLine();
            m_childProcess = true;
            m_startTime = DateTime.Now;
            Logger.Info("Server started");
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
                // A steam managed install still has the PAUSE in its launcher, which would sit
                // waiting for a keypress once the server itself is gone. Closing input gives it
                // the end of file it needs to fall through and exit.
                m_process.StandardInput.Close();
                m_process.CancelErrorRead();
                m_process.CancelOutputRead();
                m_logStream.Flush();
                m_process.WaitForExit(TimeSpan.FromSeconds(30));
            }
            if (IsRunning && m_process != null)
            {
                Logger.Info("Unable to stop server cleanly, will be killed");
                // We start the launcher rather than the server, so killing just our own child
                // would leave the actual server process running
                m_process.Kill(entireProcessTree: true);
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
