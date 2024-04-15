using Discord.Interactions;
using Newtonsoft.Json.Linq;

namespace zomboi
{
    [Group("mod", "Mod options")]
    public class ModCommandModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly HttpClient m_client = new ()
        {
            BaseAddress = new Uri("https://api.steampowered.com"),
        };

        [SlashCommand("add", "add a mod with the given id")]
        public async Task Add([Summary("id", "ID of the mod, this can be taken from the URL for the mod page in steamworkshop")] Int64 id)
        {
            var parameters = new Dictionary<string, string>();
            parameters.Add("itemcount", "1");
            parameters.Add("publishedfileids[0]", id.ToString());

            using HttpResponseMessage response = await m_client.PostAsync("ISteamRemoteStorage/GetPublishedFileDetails/v1/", new FormUrlEncodedContent(parameters));
            response.EnsureSuccessStatusCode();
            var json = JObject.Parse(await response.Content.ReadAsStringAsync());

            // Not entirely sure how the names PZ uses are decided, but from a glance it seems just like
            // it's the item's title with white space removed, so that's what I'll use until I discover otherwise
            var name = json["response"]?["publishedfiledetails"]?[0]?["title"]?.ToString();
            if (name != null)
            {
                var modName = name.Replace(" ", "");
                Server.AddMod(id, modName);
                await RespondAsync($"Added mod: {name} ({id})", ephemeral: true);
            }
            else
            {
                await RespondAsync("Error adding mod");
            }
        }
    }
}
