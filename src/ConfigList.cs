namespace zomboi
{
    /// <summary>
    /// The server's own config is a flat "key=value" file, where some of the values are comma separated
    /// lists. These edit it a line at a time, leaving the rest as the server wrote it.
    /// </summary>
    public static class ConfigList
    {
        /// <summary>
        /// The index of the line holding a key, or -1 if the file doesn't have that key.
        /// </summary>
        private static int IndexOf(string[] lines, string key)
            => Array.FindIndex(lines, line => line.TrimStart().StartsWith(key + "=", StringComparison.Ordinal));

        public static bool TryReadValue(string[] lines, string key, out string value)
        {
            value = string.Empty;
            var index = IndexOf(lines, key);
            if (index < 0)
            {
                return false;
            }
            value = lines[index][(lines[index].IndexOf('=') + 1)..].Trim();
            return true;
        }

        /// <summary>
        /// Add an entry to a comma separated value in place, leaving it alone if it's already there.
        /// Entries are compared whole, so adding "Fire" isn't refused because "Firearms" is present.
        /// </summary>
        /// <param name="added">Whether this actually changed anything, as opposed to the entry
        /// already being in the list</param>
        /// <returns>Whether the file has the key at all</returns>
        public static bool TryAdd(string[] lines, string key, string entry, out bool added)
        {
            added = false;
            var index = IndexOf(lines, key);
            if (index < 0)
            {
                return false;
            }

            var line = lines[index];
            var separator = line.IndexOf('=');
            var entries = line[(separator + 1)..]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (entries.Contains(entry, StringComparer.Ordinal))
            {
                return true;
            }

            lines[index] = line[..(separator + 1)] + string.Join(',', entries.Append(entry));
            added = true;
            return true;
        }
    }
}
