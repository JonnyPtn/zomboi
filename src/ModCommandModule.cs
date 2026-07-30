using Discord;
using Discord.Interactions;
using Newtonsoft.Json.Linq;

namespace zomboi
{
    [Group("mod", "Mod options")]
    public class ModCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static readonly HttpClient m_client = new()
        {
            BaseAddress = new Uri("https://api.steampowered.com"),
        };

        private readonly Server m_server;
        public ModCommandModule(Server server)
        {
            m_server = server;
        }

        [DefaultMemberPermissions(GuildPermission.Administrator)]
        [SlashCommand("add", "add a mod with the given id")]
        public async Task Add([Summary("id", "ID of the mod, this can be taken from the URL for the mod page in steamworkshop")] Int64 id)
        {
            var parameters = new Dictionary<string, string>
            {
                { "itemcount", "1" },
                { "publishedfileids[0]", id.ToString() }
            };

            using HttpResponseMessage response = await m_client.PostAsync("ISteamRemoteStorage/GetPublishedFileDetails/v1/", new FormUrlEncodedContent(parameters));
            response.EnsureSuccessStatusCode();
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            // Not entirely sure how the names PZ uses are decided, but from a glance it seems just like
            // it's the item's title with white space removed, so that's what I'll use until I discover otherwise
            var name = json["response"]?["publishedfiledetails"]?[0]?["title"]?.ToString();
            if (string.IsNullOrEmpty(name))
            {
                await RespondAsync($"Steam doesn't have a workshop item with the id {id}", ephemeral: true);
                return;
            }

            if (!m_server.AddMod(id, name.Replace(" ", "")))
            {
                await RespondAsync($"Couldn't add {name} ({id}), check the bot's output for why", ephemeral: true);
                return;
            }

            // The server rewrites its config on shutdown, so an edit underneath a running one is lost
            var restart = m_server.IsRunning
                ? " Restart the server for it to take effect."
                : "";
            await RespondAsync($"Added mod: {name} ({id}).{restart}", ephemeral: true);
        }
    }
}
