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
        private readonly Server m_server;
        public ChatListener(Server server) : base("*chat.txt")
        {
            m_server = server;
        }

        public async Task SetChannel(DiscordSocketClient client, string channelID)
        {
            m_client = client;
            m_channel = Channels.Find(client, channelID, "chat");
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
                // SetChannel already said why, so don't repeat it for every line the server writes
                return false;
            }
            if (!m_server.IsNews(line.TimeStamp))
            {
                return true;
            }

            // We only want to mirror general/global messages
            if (!line.Message.Contains("Got message") || !line.Message.Contains("chat=General"))
            {
                return false;
            }

            // The body comes through as text='<message>'} at the end of the line and the author as
            // author='<author>'. Read the body to the last closer, so a quote in it survives.
            if (!LogFields.TryReadField(line.Message, "text='", "'}", out var chatMessage, last: true) ||
                !LogFields.TryReadField(line.Message, "author='", "'", out var author))
            {
                Logger.Warn($"Couldn't find the author and message in: {line.Message}");
                return false;
            }
            if (chatMessage.Length == 0 || author.Length == 0)
            {
                // Discord rejects an empty message
                return true;
            }

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
}
