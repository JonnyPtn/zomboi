using System.Globalization;
using System.Numerics;

namespace zomboi
{
    /// <summary>
    /// The server writes its logs as loosely structured text, so pulling anything out of a line means
    /// hunting for brackets and quotes. These do that without throwing when a line isn't the shape we
    /// expected.
    /// </summary>
    public static class LogFields
    {
        /// <summary>
        /// The contents of each [bracketed] field in a message, in the order they appear.
        /// </summary>
        public static List<string> Bracketed(string message)
        {
            var fields = new List<string>();
            var from = 0;
            while (from < message.Length)
            {
                var open = message.IndexOf('[', from);
                if (open < 0)
                {
                    break;
                }
                var close = message.IndexOf(']', open + 1);
                if (close < 0)
                {
                    break;
                }
                fields.Add(message[(open + 1)..close]);
                from = close + 1;
            }
            return fields;
        }

        /// <summary>
        /// The text between an opening marker and a closing one.
        /// </summary>
        /// <param name="last">Match the last closing marker rather than the first, for fields whose
        /// contents could contain the marker themselves.</param>
        public static bool TryReadField(string message, string opener, string closer, out string value, bool last = false)
        {
            value = string.Empty;
            var start = message.IndexOf(opener, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }
            start += opener.Length;

            var end = last
                ? message.LastIndexOf(closer, StringComparison.Ordinal)
                : message.IndexOf(closer, start, StringComparison.Ordinal);
            if (end < start)
            {
                return false;
            }
            value = message[start..end];
            return true;
        }

        /// <summary>
        /// The one quoted section of a message, which in the player log is the player's name.
        /// </summary>
        public static bool TryReadQuotedName(string message, out string name)
        {
            name = string.Empty;
            var first = message.IndexOf('"');
            var last = message.LastIndexOf('"');
            if (first < 0 || last <= first + 1)
            {
                return false;
            }
            name = message[(first + 1)..last];
            return true;
        }

        /// <summary>
        /// A "(x,y,...)" position, which is how the player log writes one.
        /// </summary>
        public static bool TryReadPosition(string message, out Vector2 position)
        {
            position = new Vector2();
            var open = message.IndexOf('(');
            if (open < 0)
            {
                return false;
            }
            var close = message.IndexOf(')', open + 1);
            var end = close < 0 ? message.Length : close;
            return TryReadCoordinates(message[(open + 1)..end], out position);
        }

        /// <summary>
        /// A bare "x,y,..." coordinate list, which is what the perk log has in its position field. The
        /// coordinates can come through fractional, so read them as floats. Anything past the second is
        /// ignored - the third looks to be the floor they're on.
        /// </summary>
        public static bool TryReadCoordinates(string coordinates, out Vector2 position)
        {
            position = new Vector2();
            var parts = coordinates.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
            {
                return false;
            }
            position = new Vector2(x, y);
            return true;
        }

        /// <summary>
        /// The "[Hours Survived: n]" the perk log puts on the end of most of its lines, which is how
        /// long the player's current character has been alive in game rather than how long they've
        /// been playing.
        /// </summary>
        public static bool TryReadHoursSurvived(string message, out int hours)
        {
            hours = 0;
            return TryReadField(message, "Hours Survived:", "]", out var value) &&
                int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out hours);
        }
    }
}
