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
                case LogSeverity.Error:
                    Logger.Error(msg.ToString());
                    break;
                default:
                    Logger.Info(msg.ToString());
                    break;
            }
            return Task.CompletedTask;
        }

        public Zomboi()
        {
            m_configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                // Optional so a missing file is something we can explain rather than a stack trace
                .AddIniFile(ConfigFile, optional: true)
                // So the same settings can come from the environment instead, e.g. ZOMBOI_bot__token
                .AddEnvironmentVariables("ZOMBOI_")
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
        static void Main(string[] args)
            => new Zomboi().RunAsync().GetAwaiter().GetResult();

        public async Task RunAsync()
        {
            var client = m_serviceProvider.GetRequiredService<DiscordSocketClient>();
            client.Log += DiscordLog;
            m_serviceProvider.GetRequiredService<InteractionService>().Log += DiscordLog;

            await m_serviceProvider.GetRequiredService<InteractionHandler>().InitializeAsync();

            if (!File.Exists(ConfigFile))
            {
                Logger.Warn($"No {ConfigFile} next to the bot. Copy {ConfigFile}.sample to {ConfigFile} " +
                    "and fill it in, or set the settings in the environment instead.");
            }

            var token = m_configuration["bot:token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Logger.Error($"No bot token, so there's nothing to log in with. Set 'token' in the [bot] " +
                    $"section of {ConfigFile} to the token from the discord developer portal, or set the " +
                    "ZOMBOI_bot__token environment variable.");
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

                try
                {
                    m_serviceProvider.GetRequiredService<Playerlistener>().SetChannel(client, m_configuration["bot:users channel"] ?? "");
                    await m_serviceProvider.GetRequiredService<ChatListener>().SetChannel(client, m_configuration["bot:chat channel"] ?? "");
                    m_serviceProvider.GetRequiredService<PerkListener>().SetChannel(client, m_configuration["bot:skill channel"] ?? "");
                }
                catch (Exception exception)
                {
                    // Anything escaping here just gets logged by discord as an unhandled handler exception
                    DiscordError.Log("set up the bot's channels", exception);
                }

                try
                {
                    var server = m_serviceProvider.GetRequiredService<Server>();
                    if (!server.Attach())
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
