namespace zomboi
{
    /// <summary>
    /// How long a character has lasted, put the way a player would say it. The logs count in hours,
    /// which stops meaning much once there are a few days of them.
    /// </summary>
    public static class Survival
    {
        private const int HoursPerDay = 24;

        /// <summary>
        /// A survival time as days and hours, e.g. "1 day 9 hours".
        /// </summary>
        public static string Describe(int hours)
        {
            if (hours < 1)
            {
                // The perk log rounds down, so a character who has just spawned reads as zero
                return "under an hour";
            }

            var days = hours / HoursPerDay;
            var spare = hours % HoursPerDay;
            if (days == 0)
            {
                return Count(spare, "hour");
            }
            if (spare == 0)
            {
                return Count(days, "day");
            }
            return $"{Count(days, "day")} {Count(spare, "hour")}";
        }

        private static string Count(int value, string unit)
        {
            return value == 1 ? $"1 {unit}" : $"{value} {unit}s";
        }
    }
}
