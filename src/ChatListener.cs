using Discord;
using Discord.Webhook;
using Discord.WebSocket;

namespace zomboi
{
    public class ChatListener : LogFileListener
    {
        private DiscordWebhookClient? m_webhookClient;
        private IMessageChannel? m_channel;
        private DiscordSocketClient? m_client;
        private DateTime m_lastChatTime = DateTime.Now;
        public ChatListener() : base("*chat.txt")
        {
        }

        public void SetChannel(DiscordSocketClient client, string channelID)
        {
            m_client = client;
            if (channelID == null)
            {
                Logger.Error("Got a null channel ID");
                return;
            }
            var channel = client.GetChannel(ulong.Parse(channelID));
            m_channel = channel as IMessageChannel;
            if (m_channel == null)
            {
                Logger.Error($"Channel ID {channelID} does not appear to be valid");
            }

            if (m_channel is IIntegrationChannel webhookChannel)
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
            else
            {
                Logger.Error($"Unable to create webhook on channel {channelID}");
            }
        }

        override protected async Task<bool> Parse(LogLine line)
        {
            if (m_client == null || m_webhookClient == null)
            {
                Logger.Warn($"Client(s) are null, something gone wrong?");
                return false;
            }
            if (line.TimeStamp > m_lastChatTime)
            {
                m_lastChatTime = line.TimeStamp;
                // We only want to mirror general/global messages
                if (line.Message.Contains("Got message") && line.Message.Contains("chat=General"))
                {
                    // the actual message will be in the format of text='<message>' so parse for that
                    var textOpener = "text='";
                    var partial = line.Message.Substring(line.Message.IndexOf(textOpener) + textOpener.Length);
                    var chatMessage = partial.Substring(0, partial.IndexOf("'}"));

                    // and the author will be in the format author='<author>'
                    var authorOpener = "author='";
                    partial = line.Message.Substring(line.Message.IndexOf(authorOpener) + authorOpener.Length);
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
                    return true;
                }
            }
            return false;
        }
    }
}
