namespace zomboi
{
    /// <summary>
    /// The OAuth2 link that adds the bot to a discord server, built with the permissions it actually
    /// needs already ticked. The alternative is walking someone through the portal's URL generator and
    /// hoping they tick the same four boxes, which is the step people most often get wrong.
    /// </summary>
    public static class Invite
    {
        // The permission bits from https://discord.com/developers/docs/topics/permissions, and the
        // reason each one is here:
        private const ulong ViewChannel = 1024;      // finding the channels listed in zomboi.ini
        private const ulong SendMessages = 2048;     // posting the notifications
        private const ulong EmbedLinks = 16384;      // the player and server commands reply with embeds
        private const ulong ManageWebhooks = 536870912; // chat mirroring posts through a webhook

        /// <summary>
        /// Everything the bot needs, as the single number the invite url takes.
        /// </summary>
        public const ulong Permissions = ViewChannel | SendMessages | EmbedLinks | ManageWebhooks;

        /// <summary>
        /// The invite link for an application.
        /// </summary>
        /// <param name="applicationID">
        /// The application's ID. For a bot this is also the ID of its own user, so it can be read off
        /// the logged in client rather than asked for.
        /// </param>
        public static string Url(ulong applicationID)
        {
            // Both scopes: "bot" adds it to the server, "applications.commands" is what lets it
            // register the slash commands, and a link missing the second one leaves a bot that joins
            // fine and then has no commands
            return $"https://discord.com/api/oauth2/authorize?client_id={applicationID}" +
                $"&permissions={Permissions}&scope=bot%20applications.commands";
        }
    }
}
