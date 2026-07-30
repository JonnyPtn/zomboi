using Discord.Net;
using System.Net;

namespace zomboi
{
    /// <summary>
    /// Discord's API errors are pretty opaque on their own, so turn the ones we expect
    /// to hit into messages that say what actually needs fixing.
    /// </summary>
    internal static class DiscordError
    {
        // Keep these in sync with the permissions listed in the readme
        public const string RequiredScopes = "bot and applications.commands";
        public const string RequiredPermissions = "View Channel, Send Messages, Embed Links and Manage Webhooks";

        /// <summary>
        /// Log a failed discord call, saying what to do about it if we recognise the error.
        /// </summary>
        /// <param name="what">What we were trying to do, e.g. "create the chat mirroring webhook"</param>
        public static void Log(string what, Exception exception)
        {
            switch (Unwrap(exception))
            {
                case HttpException { HttpCode: HttpStatusCode.Forbidden }:
                    Logger.Error($"Discord won't let us {what} because the bot is missing permissions. " +
                        $"It needs to be invited with the {RequiredScopes} scopes, and needs {RequiredPermissions} " +
                        "on the channels in zomboi.ini. See the Discord setup section of the readme.");
                    break;
                case HttpException { HttpCode: HttpStatusCode.NotFound }:
                    Logger.Error($"Discord couldn't find what we needed to {what}. " +
                        "Check the channel IDs in zomboi.ini point at channels the bot can see.");
                    break;
                case HttpException { HttpCode: HttpStatusCode.Unauthorized }:
                    Logger.Error($"Discord rejected our token when trying to {what}. " +
                        "Check the token in zomboi.ini matches the one in the developer portal.");
                    break;
                case Exception error:
                    Logger.Error($"Failed to {what}: {error.Message}");
                    break;
            }
        }

        /// <summary>
        /// Blocking calls and event handlers wrap things in AggregateException, which buries the useful error
        /// </summary>
        private static Exception Unwrap(Exception exception)
            => exception is AggregateException { InnerExceptions.Count: 1 } aggregate
                ? Unwrap(aggregate.InnerExceptions[0])
                : exception;
    }
}
