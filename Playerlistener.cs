using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Numerics;

namespace zomboi
{
    public class Playerlistener
    {
        private readonly DiscordSocketClient m_client;
        private readonly FileSystemWatcher m_watcher;
        private FileStream? m_fileStream;
        private StreamReader? m_fileStreamReader;
        private DateTime m_lastUpdate = DateTime.Now;
        private IMessageChannel? m_channel;
        private readonly IServiceProvider m_provider;
        private readonly Server m_server;
        public Playerlistener(IServiceProvider provider)
        {
            m_provider = provider;
            m_client = provider.GetRequiredService<DiscordSocketClient>();
            m_server = provider.GetRequiredService<Server>();

            Directory.CreateDirectory(Server.LogFolderPath);
            m_watcher = new(Server.LogFolderPath)
            {
                NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite |
                NotifyFilters.Security |
                NotifyFilters.CreationTime |
                NotifyFilters.LastAccess |
                NotifyFilters.Attributes |
                NotifyFilters.Size,
                Filter = "*user.txt",
                EnableRaisingEvents = true
            };
            m_watcher.Changed += OnChanged;
            m_watcher.Created += OnChanged;
            m_watcher.Error += OnError;
        }

        public void SetChannel(string channelID)
        {
            if (channelID == null)
            {
                Logger.Error("Got a null channel ID");
                return;
            }
            var channel = m_client.GetChannel(ulong.Parse(channelID));
            m_channel = channel as IMessageChannel;
            if (m_channel == null)
            {
                Logger.Error($"Channel ID {channelID} does not appear to be valid");
            }
        }

        private async void OnChanged(object sender, FileSystemEventArgs e)
        {

            // If we haven't been given a channel then don't do anything
            if (m_channel == null)
            {
                Logger.Info("Channel not set for user notifications");
                return;
            }

            if (e.Name == null)
            {
                Logger.Error("Received a file change event without a file name");
                return;
            }

            // Update our file stream if needed
            if (m_fileStream == null || m_fileStreamReader == null || !m_fileStream.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase))
            {
                m_fileStream = new FileStream(Path.Combine(Server.LogFolderPath,e.Name), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                m_fileStreamReader = new StreamReader(m_fileStream);
            }

            var line = m_fileStreamReader.ReadLine();
            while(line != null)
            {
                LogLine logLine = new(line);
                if (logLine.TimeStamp > m_lastUpdate)
                {
                    m_lastUpdate = logLine.TimeStamp;

                    if (logLine.Message.Contains("fully connected"))
                    {
                        // The only part of this line in quotes should be the player name, so find that
                        var firstQuote = logLine.Message.IndexOf("\"");
                        var lastQuote = logLine.Message.LastIndexOf("\"");
                        var name = logLine.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);

                        // And the position will be in parentheses like (x,y,...) not sure what the last param is... floor?
                        var firstParen = logLine.Message.IndexOf("(");
                        var lastComma = logLine.Message.LastIndexOf(",");
                        var positionString = logLine.Message.Substring(firstParen + 1, lastComma - firstParen - 1); 
                        var positions = positionString.Split(',');
                        var position = new Vector2(int.Parse(positions[0]), int.Parse(positions[1]));

                        m_server.players.Add(new Player(name, logLine.TimeStamp, position));
                        await m_channel.SendMessageAsync($":wave: {name} has connected");
                    }
                    else if (logLine.Message.Contains("disconnected"))
                    {
                        // The only part of this line in quotes should be the player name, so find that
                        var firstQuote = logLine.Message.IndexOf("\"");
                        var lastQuote = logLine.Message.LastIndexOf("\"");
                        var name = logLine.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        m_server.players.RemoveAll(x => x.Name == name);
                        await m_channel.SendMessageAsync($":runner: {name} has disconnected");
                    }
                }
                line = m_fileStreamReader.ReadLine();
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            if (exception != null)
            {
                Logger.Error(exception.Message);
            }
            else
            {
                Logger.Error("Unknown file watcher error");
            }
        }
    }
}
