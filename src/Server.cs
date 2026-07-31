
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
        private const string DefaultServerName = "servertest";
        private const int DefaultPort = 16261;
        // The server asks for an administrator password on its first run, but not immediately
        private static readonly TimeSpan PasswordPromptTimeout = TimeSpan.FromMinutes(2);
        // And writes its sandbox settings out as it finishes that setup, which takes a while longer
        private static readonly TimeSpan SetupTimeout = TimeSpan.FromMinutes(5);
        // A connecting player's survival time lands in the perk log a fraction of a second after the
        // player log says they're in, so it's worth waiting for. Long enough to cover a server busy
        // enough to be slow about it, short enough that a join notification isn't left hanging if the
        // perk log never mentions them at all.
        private static readonly TimeSpan SurvivalTimeWait = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan SurvivalTimePoll = TimeSpan.FromMilliseconds(100);
        // The user data folder is the same on every platform, it's only the install that moves around
        private static readonly string m_userDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid");

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
        // Non-null only while we're waiting for the first run password prompt
        private TaskCompletionSource? m_passwordPrompt;

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
        /// What the server calls itself, which decides the name of its config file.
        /// </summary>
        public string ServerName
        {
            get
            {
                var configured = m_configuration["server:name"];
                return string.IsNullOrWhiteSpace(configured) ? DefaultServerName : configured.Trim();
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
        public string ConfigPath { get { return Path.Combine(m_userDataPath, "Server", $"{ServerName}.ini"); } }

        /// <summary>
        /// The sandbox settings, which the server only writes once its first time setup has run all
        /// the way through. The config lands early - before it has even asked for an administrator
        /// password - so the config can't tell us the setup finished, and this can.
        /// </summary>
        public string SandboxPath { get { return Path.Combine(m_userDataPath, "Server", $"{ServerName}_SandboxVars.lua"); } }

        /// <summary>
        /// Whether there's a config to read the server's settings out of. Written early in a first
        /// run, so see <see cref="IsCreated"/> for whether that run actually got to the end.
        /// </summary>
        public bool HasConfig { get { return File.Exists(ConfigPath); } }
        public bool IsCreated { get { return File.Exists(SandboxPath); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(m_userDataPath, "Logs")); } }

        /// <summary>
        /// The port the server listens on, from its own config so one set up outside the bot is right too.
        /// </summary>
        public int Port
        {
            get
            {
                try
                {
                    if (HasConfig &&
                        ConfigList.TryReadValue(File.ReadAllLines(ConfigPath), "DefaultPort", out var value) &&
                        int.TryParse(value, out var port))
                    {
                        return port;
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warn($"Couldn't read the port from {ConfigPath}: {exception.Message}");
                }
                return DefaultPort;
            }
        }

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
        private DateTime m_notifyFrom = DateTime.Now;
        private IMessageChannel? m_channel;

        public Server(DiscordSocketClient client, IConfiguration configuration)
        {
            m_client = client;
            m_configuration = configuration;
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            m_channel = Channels.Find(client, channelID, "server");
        }

        /// <summary>
        /// Tell the server channel the server has come up or gone down, if one is set.
        /// </summary>
        private async Task Announce(string message)
        {
            if (m_channel == null)
            {
                return;
            }
            try
            {
                await m_channel.SendMessageAsync(message);
            }
            catch (Exception exception)
            {
                // The server itself is up or down either way, so a failed notification isn't a
                // failed start or stop
                DiscordError.Log($"post '{message}' to the server channel", exception);
            }
        }

        public delegate Task PlayerJoined(Player player);
        public event PlayerJoined? OnPlayerJoined;

        public delegate Task PlayerDeath(Player player);
        public event PlayerDeath? OnPlayerDied;

        // Both the output and error handlers write to this from their own threads
        private readonly TextWriter m_logStream = TextWriter.Synchronized(
            new StreamWriter(new FileStream("server.log", FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            {
                // Without this the log sits empty until the buffer fills
                AutoFlush = true
            });

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
        /// Record where a player is and how long their character has lasted, which is what the perk log
        /// reports on every line it writes about them. Not the same as them connecting - it says
        /// nothing about whether they're online - so nobody is told about it.
        /// </summary>
        public Player PlayerSeen(string playerName, DateTime seenTime, Vector2? position, int? hoursSurvived)
        {
            var player = GetOrCreatePlayer(playerName, seenTime);
            lock (m_playersLock)
            {
                if (position != null)
                {
                    player.Position = position.Value;
                }
                if (hoursSurvived != null)
                {
                    player.HoursSurvived = hoursSurvived;
                }
            }
            return player;
        }

        /// <summary>
        /// How long a player's character has survived, giving the perk log a moment to say if we don't
        /// know yet. Only that log carries the number and it writes it just after the player log says
        /// they're connected, so anything reporting on a join has to wait for it rather than read it
        /// straight away. Null if it never turns up.
        /// </summary>
        public async Task<int?> WaitForHoursSurvived(Player player)
        {
            // A player we've seen before this session has a survival time already, and it can't have
            // moved on while they were away, so there's nothing to wait for
            await WaitFor(() => player.HoursSurvived != null, SurvivalTimeWait, SurvivalTimePoll);
            return player.HoursSurvived;
        }

        /// <summary>
        /// Whether something the logs say happened then is news, or history from before we were
        /// watching. We read them from the top, so attaching to a server that's been up for hours reads
        /// hours of joins, deaths and level ups that nobody wants notifying about now.
        /// </summary>
        public bool IsNews(DateTime when)
        {
            return when > m_notifyFrom;
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
                // If it was before we were watching then it's history, not someone joining now
                joined = !player.Online && IsNews(seenTime);
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
        /// Record a player's character dying, telling anyone listening about it. Dying doesn't take them
        /// offline - they're still connected, picking a new character - so nothing else changes.
        /// </summary>
        public async Task PlayerDied(Player player, DateTime diedTime)
        {
            // If it was before we were watching then it's a character they lost while we weren't
            // looking, which the log still tells us about but nobody wants announced now
            if (!IsNews(diedTime) || OnPlayerDied == null)
            {
                return;
            }

            // Invoke the handlers by hand so each one is awaited, rather than just the last
            foreach (PlayerDeath handler in OnPlayerDied.GetInvocationList())
            {
                await handler(player);
            }
        }

        /// <summary>
        /// Record a player disconnecting.
        /// </summary>
        /// <returns>Whether they've just left, so we don't announce the same player leaving twice or
        /// one who left before we were watching at all</returns>
        public bool PlayerDisconnected(string playerName, DateTime seenTime, Vector2? position)
        {
            var player = GetOrCreatePlayer(playerName, seenTime);
            lock (m_playersLock)
            {
                if (player.LastSeen > seenTime)
                {
                    return false;
                }
                player.LastSeen = seenTime;
                if (position != null)
                {
                    player.Position = position.Value;
                }
                if (!player.Online)
                {
                    return false;
                }
                player.Online = false;
                return IsNews(seenTime);
            }
        }

        /// <summary>
        /// What a running server is called in the process list. On windows the launcher runs the jre
        /// the server ships with rather than an executable of its own, so what we're looking for
        /// there is a plain jvm - <see cref="IsOurServer"/> is what tells it apart from any other
        /// java on the box.
        /// </summary>
        private static string[] ServerProcessNames
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new[] { "java" }
                    : new[] { "ProjectZomboid64" };
            }
        }

        /// <summary>
        /// Whether that process is the server out of the install we'd start ourselves, rather than
        /// an unrelated jvm or a second server running from somewhere else.
        /// </summary>
        private bool IsOurServer(Process process)
        {
            var startPath = StartPath;
            if (startPath == null)
            {
                return false;
            }

            // With the separator on the end, so a sibling folder that merely starts with the same
            // name isn't taken for ours
            var folder = Path.GetFullPath(Path.GetDirectoryName(startPath) ?? string.Empty);
            if (!folder.EndsWith(Path.DirectorySeparatorChar))
            {
                folder += Path.DirectorySeparatorChar;
            }

            try
            {
                var executable = process.MainModule?.FileName;
                return executable != null && executable.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                // A process we can't look inside isn't one we could drive either way
                Logger.Warn($"Couldn't tell what process {process.Id} is running: {exception.Message}");
                return false;
            }
        }

        public async Task<bool> Attach()
        {
            var existing = ServerProcessNames.SelectMany(Process.GetProcessesByName).ToArray();
            var ours = existing.Where(IsOurServer).ToArray();
            var attached = false;
            try
            {
                if (ours.Length > 1)
                {
                    Logger.Error($"Found {ours.Length} existing server processes");
                }
                else if (ours.Length == 1)
                {
                    m_process = ours[0];
                    m_childProcess = false;
                    // The server was up before we were, so its own start is the honest one to report
                    m_startTime = m_process.StartTime;
                    // Its logs go back that far too, but only what happens from now on is news
                    m_notifyFrom = DateTime.Now;
                    Logger.Info("Attached to server process");
                    attached = true;
                }
            }
            finally
            {
                foreach (var process in existing.Where(process => process != m_process))
                {
                    process.Dispose();
                }
            }

            if (attached)
            {
                await Announce(":green_circle: Attached to the running server");
            }
            return attached;
        }

        public Task<bool> Start()
        {
            // Create runs the server through its first setup by starting it itself, and hasn't
            // written the config by that point, so this guard belongs out here rather than in
            // Start(announce). A missing install is left to that, which explains itself better.
            if (IsInstalled && !IsCreated)
            {
                Logger.Error($"{ServerName} hasn't been set up yet, so it would sit waiting for an " +
                    "administrator password instead of coming up. Use /zomboi create <password> first.");
                return Task.FromResult(false);
            }
            return Start(announce: true);
        }

        /// <param name="announce">Whether to tell the server channel about it, which the first time
        /// setup doesn't want as the server isn't up for anyone to join yet</param>
        private async Task<bool> Start(bool announce)
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
            // Only pass it on when configured, so the default stays whatever the server would pick
            if (!string.IsNullOrWhiteSpace(m_configuration["server:name"]))
            {
                m_process.StartInfo.ArgumentList.Add("-servername");
                m_process.StartInfo.ArgumentList.Add(ServerName);
            }
            m_process.OutputDataReceived += (sender, e) => OnServerOutput(e.Data);
            m_process.ErrorDataReceived += (sender, e) => OnServerOutput(e.Data);
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
            // The server only reads whole lines, so don't let anything we write sit in a buffer
            m_process.StandardInput.AutoFlush = true;
            m_process.BeginErrorReadLine();
            m_process.BeginOutputReadLine();
            m_childProcess = true;
            m_startTime = DateTime.Now;
            // The logs it appends to can still hold an earlier session, so not all of them are news
            m_notifyFrom = m_startTime;
            Logger.Info("Server started");
            await m_client.SetGameAsync("Project Zomboid");
            await m_client.SetStatusAsync(UserStatus.Online);
            if (announce)
            {
                await Announce(":green_circle: Server started");
            }
            return true;
        }

        /// <summary>
        /// Everything the server writes goes to server.log, and on a first run we watch it go by for
        /// the password prompt.
        /// </summary>
        private void OnServerOutput(string? data)
        {
            if (data == null)
            {
                return;
            }
            m_logStream.WriteLine(data);
            if (m_passwordPrompt != null && data.Contains("administrator password", StringComparison.OrdinalIgnoreCase))
            {
                m_passwordPrompt.TrySetResult();
            }
        }

        public Task<bool> Stop()
        {
            return Stop(announce: true);
        }

        /// <param name="announce">Whether to tell the server channel about it, which the first time
        /// setup doesn't want as nobody was told it started either</param>
        private async Task<bool> Stop(bool announce)
        {
            if (!IsRunning || m_process == null)
            {
                Logger.Warn("Trying to stop a server that isn't running");
                return false;
            }

            await m_client.SetGameAsync(null);
            await m_client.SetStatusAsync(UserStatus.DoNotDisturb);
            if (m_childProcess)
            {
                try
                {
                    await m_process.StandardInput.WriteLineAsync("save");
                    await m_process.StandardInput.WriteLineAsync("quit");
                    // A steam managed install still has the PAUSE in its launcher, which would sit
                    // waiting for a keypress once the server itself is gone. Closing input gives it
                    // the end of file it needs to fall through and exit.
                    m_process.StandardInput.Close();
                    m_process.CancelErrorRead();
                    m_process.CancelOutputRead();
                    m_process.WaitForExit(TimeSpan.FromSeconds(30));
                }
                catch (Exception exception)
                {
                    // If it died between the check and the quit there's nothing to save, but we
                    // still owe it a kill and a tidy up below
                    Logger.Warn($"Couldn't shut the server down cleanly: {exception.Message}");
                }
            }
            if (IsRunning)
            {
                Logger.Info("Unable to stop server cleanly, will be killed");
                // We start the launcher rather than the server, so killing just our own child
                // would leave the actual server process running
                m_process.Kill(entireProcessTree: true);
                m_process.WaitForExit(TimeSpan.FromSeconds(10));
            }
            Logger.Info("Server stopped");
            m_childProcess = false;
            m_process.Dispose();
            m_process = null;
            if (announce)
            {
                await Announce(":red_circle: Server stopped");
            }
            return true;
        }

        /// <summary>
        /// Run the server through its first time setup, which is a password prompt and then a long wait
        /// while it generates the world and writes its config out.
        /// </summary>
        public async Task<bool> Create(string password)
        {
            if (IsRunning)
            {
                Logger.Error("Can't create the server while it's running, stop it first");
                return false;
            }
            if (IsCreated)
            {
                Logger.Error($"{ServerName} has already been created. Delete {ConfigPath} and " +
                    $"{SandboxPath} first if you want to start it over from scratch.");
                return false;
            }

            // Watch for the prompt before starting, so we can't miss it
            m_passwordPrompt = new TaskCompletionSource();
            try
            {
                if (!await Start(announce: false) || m_process == null)
                {
                    return false;
                }

                // A prompt written without a newline after it never reaches us, so give it a fair
                // chance to show up and then answer regardless
                if (await Task.WhenAny(m_passwordPrompt.Task, Task.Delay(PasswordPromptTimeout)) != m_passwordPrompt.Task)
                {
                    Logger.Warn($"Didn't see the password prompt within {PasswordPromptTimeout.TotalMinutes:0} " +
                        "minutes, answering it anyway");
                }
                await m_process.StandardInput.WriteLineAsync(password); // Set the password
                await m_process.StandardInput.WriteLineAsync(password); // Confirm the password
            }
            finally
            {
                m_passwordPrompt = null;
            }

            // The sandbox settings only turn up once it's run all the way through its setup, so
            // stopping before then would leave a half made server. Give up early if it dies rather
            // than waiting out the whole timeout.
            await WaitFor(() => IsCreated || !IsRunning, SetupTimeout);
            var created = IsCreated;
            await Stop(announce: false);
            if (!created)
            {
                Logger.Error($"The server never wrote {SandboxPath}, so it isn't set up. Check server.log " +
                    "for what it said, and that the password meets whatever the server asked for.");
                return false;
            }
            Logger.Info("Server created");
            return true;
        }

        private static async Task<bool> WaitFor(Func<bool> condition, TimeSpan timeout, TimeSpan? interval = null)
        {
            var deadline = DateTime.Now + timeout;
            while (DateTime.Now < deadline)
            {
                if (condition())
                {
                    return true;
                }
                await Task.Delay(interval ?? TimeSpan.FromSeconds(1));
            }
            return condition();
        }

        /// <summary>
        /// Add a workshop item to the server's config, where the ids go in WorkshopItems and the names
        /// in Mods, both as comma separated lists.
        /// </summary>
        public bool AddMod(Int64 modID, string modName)
        {
            if (!HasConfig)
            {
                Logger.Error("Can't add mod before server is created");
                return false;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(ConfigPath);
            }
            catch (Exception exception)
            {
                Logger.Error($"Couldn't read {ConfigPath}: {exception.Message}");
                return false;
            }

            // Both have to be there, or we'd write out a server that loads a mod it has no id for
            if (!ConfigList.TryAdd(lines, "WorkshopItems", modID.ToString(), out var idAdded) ||
                !ConfigList.TryAdd(lines, "Mods", modName, out var nameAdded))
            {
                Logger.Error($"{ConfigPath} doesn't have both a WorkshopItems and a Mods line, so " +
                    $"{modName} ({modID}) wasn't added");
                return false;
            }
            if (!idAdded && !nameAdded)
            {
                Logger.Warn($"Mod already added: {modName} ({modID})");
                return true;
            }

            try
            {
                File.WriteAllLines(ConfigPath, lines);
            }
            catch (Exception exception)
            {
                Logger.Error($"Couldn't write {ConfigPath}: {exception.Message}");
                return false;
            }
            Logger.Info($"Mod added: {modName} ({modID})");
            return true;
        }
    }
}
