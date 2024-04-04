using Discord;
using Discord.Interactions;
using Discord.Net;
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
            GatewayIntents = GatewayIntents.All,
            AlwaysDownloadUsers = true
        };

        private static Task DiscordLog(LogMessage msg)
        {
            switch(msg.Severity)
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
                .AddEnvironmentVariables(prefix: "ZOMBOI_")
                .Build();

            m_serviceProvider = new ServiceCollection()
                .AddSingleton(m_configuration)
                .AddSingleton(m_socketConfig)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>()
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
            var token = m_configuration["TOKEN"];

            if (token == null)
            {
                Logger.Error("Token not found, set ZOMBOI_TOKEN environment variable to your discord bot token");
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

            await client.LoginAsync(TokenType.Bot, token); 
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }
    }
}
