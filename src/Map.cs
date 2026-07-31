using System.Globalization;
using System.Numerics;

namespace zomboi
{
    /// <summary>
    /// Links into the community map at map.projectzomboid.com, which reads the point to show out of its
    /// own query string as "?<x>x<y>" and takes the same world coordinates the server logs report.
    /// </summary>
    public static class Map
    {
        private const string BaseUrl = "https://map.projectzomboid.com/";

        /// <summary>
        /// The map centred on that position.
        /// </summary>
        public static string Url(Vector2 position)
        {
            return $"{BaseUrl}?{Coordinate(position.X)}x{Coordinate(position.Y)}";
        }

        // The pin sits outside the link rather than being it. Discord used to render a masked link
        // wrapped around an emoji and doesn't any more, so a pin on its own comes out as the raw
        // "[pin](url)" markdown - see https://github.com/discord/discord-api-docs/issues/6116. A
        // literal emoji rather than a :round_pushpin: shortcode, which isn't expanded everywhere.
        private const string Pin = "\U0001F4CD";
        // Something has to be worded, and coordinates in the middle of a sentence say very little to
        // anyone reading it
        private const string Label = "map";

        /// <summary>
        /// A discord link to that position, as a pin and a word to click. Hovering it still shows the
        /// coordinates, since they're in the url.
        /// </summary>
        public static string Link(Vector2 position)
        {
            return $"{Pin}[{Label}]({Url(position)})";
        }

        /// <summary>
        /// A coordinate as the url wants it, which is whole even though the logs can report a fractional
        /// position. A string so the url can't pick up whatever digits the current culture would rather
        /// use.
        /// </summary>
        private static string Coordinate(float value)
        {
            return ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture);
        }
    }
}
