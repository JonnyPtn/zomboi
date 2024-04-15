

using System.Globalization;

namespace zomboi
{
    public struct LogLine
    {
        public LogLine(string line)
        {
            Message = line.Substring(line.IndexOf(']') + 2);
            try
            {
                // Format should be "[timestamp] message" so split based on that
                var timestampStr = line.Substring(line.IndexOf('[') + 1, line.IndexOf(']') - line.IndexOf('[') - 1);
                TimeStamp = DateTime.ParseExact(timestampStr, "dd-MM-yy HH:mm:ss.fff", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }
        public DateTime TimeStamp { get; }
        public string Message { get; }

        public static implicit operator string(LogLine line)
        {
            return $"[{line.TimeStamp}] {line.Message}";
        }
    }
}
