using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace zomboi
{
    public class UserListener
    {
        private readonly DiscordSocketClient m_client;
        private readonly FileSystemWatcher m_watcher;
        private FileStream? m_fileStream;
        private StreamReader? m_fileStreamReader;
        private DateTime m_lastUpdate = DateTime.Now;
        private IMessageChannel? m_channel;
        public UserListener(DiscordSocketClient client)
        {
            m_client = client;

            m_watcher = new(Server.LogFolderPath);
            m_watcher.NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.DirectoryName |
                NotifyFilters.LastWrite |
                NotifyFilters.Security |
                NotifyFilters.CreationTime |
                NotifyFilters.LastAccess |
                NotifyFilters.Attributes |
                NotifyFilters.Size;
            m_watcher.Filter = "*user.txt";
            m_watcher.EnableRaisingEvents = true;
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
            m_channel = m_client.GetChannel(ulong.Parse(channelID)) as IMessageChannel;
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
                        await m_channel.SendMessageAsync($":wave: {name} has connected");
                    }
                    else if (logLine.Message.Contains("disconnected"))
                    {
                        // The only part of this line in quotes should be the player name, so find that
                        var firstQuote = logLine.Message.IndexOf("\"");
                        var lastQuote = logLine.Message.LastIndexOf("\"");
                        var name = logLine.Message.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
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
