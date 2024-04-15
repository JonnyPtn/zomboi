using zomboi;

abstract public class LogFileListener
{
    private readonly FileSystemWatcher m_watcher;
    private StreamReader? m_reader;
    private DateTime m_last_update = DateTime.UnixEpoch;
    public LogFileListener(string filePattern)
    {
        Directory.CreateDirectory(Server.LogFolderPath);
        m_watcher = new(Server.LogFolderPath)
        {
            NotifyFilter =
            NotifyFilters.FileName |
            NotifyFilters.DirectoryName |
            NotifyFilters.LastWrite |
            NotifyFilters.Security |
            NotifyFilters.CreationTime |
            NotifyFilters.LastAccess |
            NotifyFilters.Attributes |
            NotifyFilters.Size,
            Filter = filePattern,
            EnableRaisingEvents = true
        };
        m_watcher.Changed += OnChanged;
        m_watcher.Created += OnCreated;
        m_watcher.Error += OnError;
    }
    abstract protected Task<bool> Parse(LogLine line);

    private async Task Update()
    {
        string? line;
        while ((line = m_reader?.ReadLine()) != null)
        {
            LogLine logLine = new(line);
            if (logLine.TimeStamp >= m_last_update)
            {
                m_last_update = logLine.TimeStamp;

                if (!await Parse(logLine))
                {
                    Logger.Info($"Unhandled Log: {logLine.Message}");
                }
            }
        }
    }
    async void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name == null)
        {
            Logger.Error("Received a file change event without a file name");
            return;
        }

        if (m_reader == null)
        {
            OnCreated(sender, e);
        }

        await Update();
    }
    void OnCreated(object sender, FileSystemEventArgs e)
    {
        var fileStream = new FileStream(e.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        {
            Position = 0
        };
        m_reader = new StreamReader(fileStream);
        Logger.Info($"Watching new log file: {e.FullPath}");
    }
    void OnError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        if (exception != null)
        {
            Logger.Error(exception.Message);
        }
        else
        {
            Logger.Error("Unknown file watcher error");
        }
    }
}