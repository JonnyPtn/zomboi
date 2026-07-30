using System.Globalization;

namespace zomboi
{
    /// <summary>
    /// One "[dd-MM-yy HH:mm:ss.fff] message" line from a server log.
    /// </summary>
    public struct LogLine
    {
        private const string TimeStampFormat = "dd-MM-yy HH:mm:ss.fff";

        private LogLine(DateTime timeStamp, string message)
        {
            TimeStamp = timeStamp;
            Message = message;
        }

        /// <summary>
        /// Read a log line, failing if it hasn't got the timestamp prefix - plenty of what the server
        /// writes hasn't, so an unreadable line isn't worth complaining about.
        /// </summary>
        public static bool TryParse(string line, out LogLine logLine)
        {
            logLine = new LogLine(DateTime.UnixEpoch, string.Empty);

            var open = line.IndexOf('[');
            if (open < 0)
            {
                return false;
            }
            var close = line.IndexOf(']', open + 1);
            if (close < 0)
            {
                return false;
            }
            if (!DateTime.TryParseExact(line[(open + 1)..close], TimeStampFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeStamp))
            {
                return false;
            }

            logLine = new LogLine(timeStamp, line[(close + 1)..].TrimStart());
            return true;
        }

        public DateTime TimeStamp { get; }
        public string Message { get; }

        public static implicit operator string(LogLine line)
        {
            return $"[{line.TimeStamp}] {line.Message}";
        }
    }
}
