using Discord;
using Discord.Interactions;
using Discord.Webhook;
using Discord.WebSocket;

namespace zomboi
{
    public class ChatListener
    {
        private readonly DiscordSocketClient m_client;
        private DiscordWebhookClient? m_webhookClient;
        private readonly FileSystemWatcher m_watcher;
        private FileStream? m_fileStream;
        private StreamReader? m_fileStreamReader;
        private DateTime m_lastUpdate = DateTime.Now;
        private IMessageChannel? m_channel;
        public ChatListener(DiscordSocketClient client)
        {
            m_client = client;

            m_watcher = new(Server.LogFolderPath);
            m_watcher.NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.CreationTime |
                NotifyFilters.LastAccess |
                NotifyFilters.Size;
            m_watcher.Filter = "*chat.txt";
            m_watcher.EnableRaisingEvents = true;
            m_watcher.Changed += OnChanged;
            m_watcher.Created += OnCreated;
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

            var webhookChannel = m_channel as IIntegrationChannel;
            if (webhookChannel == null)
            {
                Logger.Error($"Unable to create webhook on channel {channelID}");
            }
            else
            {
                // Possibly naive to assume we're the only webhook on the channel
                var webhooks = webhookChannel.GetWebhooksAsync().Result;
                if (webhooks.Count == 0)
                {
                    var webhook = webhookChannel.CreateWebhookAsync("zomboi").Result;
                    m_webhookClient = new DiscordWebhookClient(webhook);
                }
                else
                {
                    m_webhookClient = new DiscordWebhookClient(webhooks.First());
                }
            }
        }

        private async void OnCreated(object sender, FileSystemEventArgs e)
        {
            m_fileStream = new FileStream(Path.Combine(Server.LogFolderPath,e.FullPath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            m_fileStreamReader = new StreamReader(m_fileStream);
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

            if (m_webhookClient == null)
            {
                Logger.Error("Webhook client is null");
                m_watcher.EnableRaisingEvents = false;
                return;
            }

            if (m_fileStream == null || m_fileStreamReader == null)
            {
                Logger.Error("File stream not opened");
                return;
            }

            if(!m_fileStream.Name.Equals(e.FullPath, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"unexpected file chamge: {e.Name}");
            }

            var line = m_fileStreamReader.ReadLine();
            while(line != null)
            {
                LogLine logLine = new(line);
                if (logLine.TimeStamp > m_lastUpdate)
                {
                    m_lastUpdate = logLine.TimeStamp;

                    // We only want to mirror general/global messages
                    if (logLine.Message.Contains("chat=General"))
                    {
                        // the actual message will be in the format of text='<message>' so parse for that
                        var textOpener = "text='";
                        var partial = logLine.Message.Substring(logLine.Message.IndexOf(textOpener) + textOpener.Length);
                        var chatMessage = partial.Substring(0,partial.IndexOf("'}"));

                        // and the author will be in the format author='<author>'
                        var authorOpener = "author='";
                        partial = logLine.Message.Substring(logLine.Message.IndexOf(authorOpener) + authorOpener.Length);
                        var author = partial.Substring(0, partial.IndexOf("'"));

                        // Check if there's a discord user with a matching name, and use their picture if so
                        var user = m_client.GetUser(author);
                        if (user != null)
                        {
                            await m_webhookClient.SendMessageAsync(text: chatMessage, username: user.GlobalName, avatarUrl: user.GetAvatarUrl());
                        }
                        else
                        {
                            await m_webhookClient.SendMessageAsync(text: chatMessage, username: author, avatarUrl: m_client.CurrentUser.GetAvatarUrl());
                        }

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
