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

        public async Task SetChannel(DiscordSocketClient client, string channelID)
        {
            m_client = client;
            m_channel = FindChannel(client, channelID, "chat");
            if (m_channel == null)
            {
                return;
            }

            if (m_channel is not IIntegrationChannel webhookChannel)
            {
                Logger.Error($"Channel {channelID} doesn't support webhooks, so chat can't be mirrored to it");
                return;
            }

            try
            {
                // Possibly naive to assume we're the only webhook on the channel
                var webhooks = await webhookChannel.GetWebhooksAsync();
                var webhook = webhooks.FirstOrDefault() ?? await webhookChannel.CreateWebhookAsync("zomboi");
                m_webhookClient = new DiscordWebhookClient(webhook);
            }
            catch (Exception exception)
            {
                // Reading and creating webhooks both need the Manage Webhooks permission
                DiscordError.Log($"set up chat mirroring on channel {channelID}", exception);
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
