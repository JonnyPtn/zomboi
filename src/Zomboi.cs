using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace zomboi
{
    public class Zomboi
    {
        private readonly IConfiguration m_configuration;
        private readonly IServiceProvider m_serviceProvider;

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
                .AddIniFile("zomboi.ini")
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

            // Set the ZOMBOI_TOKEN environment variable to the discord bot token
            var token = m_configuration["bot:token"];

            if (token == null)
            {
                Logger.Error("Token cannot be null");
                return;
            }
            else if (token.Length == 0)
            {
                Logger.Error("Token is empty");
                return;
            }
            else if (token.Trim().Length == 0)
            {
                Logger.Error("Token is only whitespace");
                return;
            }

            // Once we're logged in, set up our channels
            client.Ready += async () =>
            {
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

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }
    }
}
