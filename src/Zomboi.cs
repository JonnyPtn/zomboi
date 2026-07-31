using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    public class Zomboi
    {
        private const string ConfigFile = "zomboi.ini";
        private const string ConfigArgument = "--config";

        private readonly string m_configFile;
        private readonly IConfiguration m_configuration;
        private readonly IServiceProvider m_serviceProvider;
        // Ready fires again after every reconnect, and none of the setup wants doing twice
        private int m_setUp = 0;

        private readonly DiscordSocketConfig m_socketConfig = new()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
            AlwaysDownloadUsers = true
        };

        private static Task DiscordLog(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    if (msg.Exception is null)
                    {
                        Logger.Error(msg.ToString());
                        break;
                    }
                    // Discord.Net logs its own failures as a bare stack trace, which says what broke
                    // but not what to do about it
                    var where = string.IsNullOrWhiteSpace(msg.Message) ? msg.Source : $"{msg.Source}: {msg.Message}";
                    DiscordError.Log($"talk to discord ({where})", msg.Exception);
                    break;
                default:
                    Logger.Info(msg.ToString());
                    break;
            }
            return Task.CompletedTask;
        }

        public Zomboi(string configFile)
        {
            m_configFile = configFile;
            m_configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(configFile) ?? Directory.GetCurrentDirectory())
                // Optional so a missing file is something we can explain rather than a stack trace
                .AddIniFile(Path.GetFileName(configFile), optional: true)
                .Build();

            m_serviceProvider = new ServiceCollection()
                .AddSingleton(m_configuration)
                .AddSingleton(m_socketConfig)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<Server>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>()
                .AddSingleton<Playerlistener>()
                .AddSingleton<ChatListener>()
                .AddSingleton<PerkListener>()
                .BuildServiceProvider();
        }

        private static void Usage()
        {
            Logger.Info(string.Join(Environment.NewLine, new[]
            {
                $"Usage: zomboi [{ConfigArgument} <path>]",
                "",
                $"  {ConfigArgument} <path>   The settings file to read, and the folder the bot works in.",
                $"                    Defaults to a {ConfigFile} in the current folder, then one next",
                "                    to the bot itself."
            }));
        }

        /// <summary>
        /// The value given to <see cref="ConfigArgument"/>, in either the "--config path" or
        /// "--config=path" spelling, or null if it wasn't passed at all.
        /// </summary>
        private static string? ConfigOption(string[] args)
        {
            for (var index = 0; index < args.Length; index++)
            {
                if (args[index].StartsWith($"{ConfigArgument}=", StringComparison.Ordinal))
                {
                    return args[index][(ConfigArgument.Length + 1)..];
                }
                if (args[index] == ConfigArgument)
                {
                    return index + 1 < args.Length ? args[index + 1] : "";
                }
            }
            return null;
        }

        /// <summary>
        /// The settings file to read, which also settles the folder the bot runs from. In order:
        /// whatever <see cref="ConfigArgument"/> points at, an existing file in the current folder,
        /// then one next to the executable - so a bot started as a service, from a shortcut, or by any
        /// other route that doesn't set a sensible working directory still finds its settings. Falls
        /// back to the current folder, which gives a first run somewhere to be told to put one.
        /// </summary>
        /// <exception cref="ArgumentException">The path given on the command line is unusable.</exception>
        private static string FindConfig(string[] args)
        {
            var option = ConfigOption(args);
            if (option != null)
            {
                if (string.IsNullOrWhiteSpace(option))
                {
                    throw new ArgumentException($"{ConfigArgument} needs the path to a settings file after it.");
                }
                // Asked for a specific file, so a missing one is a mistake worth stopping for rather
                // than something to quietly fall back from
                var path = Path.GetFullPath(option.Trim());
                if (!File.Exists(path))
                {
                    throw new ArgumentException($"No settings file at {path}.");
                }
                return path;
            }

            var candidates = new[]
            {
                Path.GetFullPath(ConfigFile),
                Path.Combine(AppContext.BaseDirectory, ConfigFile)
            };
            return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        }

        static int Main(string[] args)
        {
            if (args.Any(argument => argument is "--help" or "-h" or "/?"))
            {
                Usage();
                return 0;
            }

            string configFile;
            try
            {
                configFile = FindConfig(args);
            }
            catch (ArgumentException exception)
            {
                Logger.Error(exception.Message);
                Usage();
                return 1;
            }

            // The rest of the bot reaches for steamcmd, the server folder and its own log by relative
            // path, so move to where the settings live once here rather than rebasing each of them.
            // Without this, running from anywhere but the install folder scatters those around.
            var home = Path.GetDirectoryName(configFile);
            if (!string.IsNullOrEmpty(home) && home != Directory.GetCurrentDirectory())
            {
                Directory.SetCurrentDirectory(home);
            }

            new Zomboi(configFile).RunAsync().GetAwaiter().GetResult();
            return 0;
        }

        public async Task RunAsync()
        {
            var client = m_serviceProvider.GetRequiredService<DiscordSocketClient>();
            client.Log += DiscordLog;
            m_serviceProvider.GetRequiredService<InteractionService>().Log += DiscordLog;

            await m_serviceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

            if (!File.Exists(m_configFile))
            {
                Logger.Warn($"No settings file at {m_configFile}. Copy {ConfigFile}.sample to {ConfigFile} " +
                    "and fill it in.");
            }

            var token = m_configuration["bot:token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.Error($"No bot token, so there's nothing to log in with. Set 'token' in the [bot] " +
                    $"section of {m_configFile} to the token from the discord developer portal.");
                return;
            }
            token = token.Trim();

            try
            {
                // Discord.Net only warns about a token it can see is wrong, then spends forever
                // failing to connect with it, so check it ourselves while we can still say why
                TokenUtils.ValidateToken(TokenType.Bot, token);
            }
            catch (Exception exception)
            {
                Logger.Error($"That doesn't look like a bot token: {exception.Message} Take it from the Bot " +
                    "page of the discord developer portal - it isn't the application ID, the public key or " +
                    "the OAuth2 client secret.");
                return;
            }

            // Once we're logged in, set up our channels
            client.Ready += async () =>
            {
                if (Interlocked.Exchange(ref m_setUp, 1) != 0)
                {
                    return;
                }

                // A bot that isn't in a server can't do anything, and every channel lookup below is
                // about to fail for that one reason, so say so first and hand over the link that
                // fixes it. Its own user ID is the application ID the invite needs, which saves
                // anyone going back to the portal to look it up.
                if (client.Guilds.Count == 0)
                {
                    Logger.Warn($"{client.CurrentUser.Username} isn't in any discord server yet. Invite it " +
                        $"with this link, which already asks for the permissions it needs:" +
                        $"{Environment.NewLine}{Invite.Url(client.CurrentUser.Id)}");
                }

                try
                {
                    m_serviceProvider.GetRequiredService<Playerlistener>().SetChannel(client, m_configuration["bot:users channel"] ?? "");
                    await m_serviceProvider.GetRequiredService<ChatListener>().SetChannel(client, m_configuration["bot:chat channel"] ?? "");
                    m_serviceProvider.GetRequiredService<PerkListener>().SetChannel(client, m_configuration["bot:skill channel"] ?? "");
                    // Before the server starts below, so the notification for that start isn't lost
                    m_serviceProvider.GetRequiredService<Server>().SetChannel(client, m_configuration["bot:server channel"] ?? "");
                }
                catch (Exception exception)
                {
                    // Anything escaping here just gets logged by discord as an unhandled handler exception
                    DiscordError.Log("set up the bot's channels", exception);
                }

                try
                {
                    var server = m_serviceProvider.GetRequiredService<Server>();
                    if (!await server.Attach())
                    {
                        await server.Start();
                    }
                }
                catch (Exception exception)
                {
                    DiscordError.Log("start the game server", exception);
                }
            };

            try
            {
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();
            }
            catch (Exception exception)
            {
                // Whatever else stops us connecting, a stack trace doesn't say what to fix
                DiscordError.Log("log in to discord", exception);
                return;
            }

            await Task.Delay(Timeout.Infinite);
        }
    }
}
