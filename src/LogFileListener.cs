using zomboi;

abstract public class LogFileListener : IDisposable
{
    private readonly FileSystemWatcher m_watcher;
    // The watcher raises its events on thread pool threads, and one write can raise several, so the
    // handlers would otherwise read from the same reader at once
    private readonly SemaphoreSlim m_lock = new(1, 1);
    private StreamReader? m_reader;
    private DateTime m_last_update = DateTime.UnixEpoch;
    public LogFileListener(string filePattern)
    {
        Directory.CreateDirectory(Server.LogFolderPath);
        m_watcher = new(Server.LogFolderPath)
        {
            // Appends show up as size and last write changes, which is all we need. Watching the
            // rest just multiplies the events per write.
            NotifyFilter =
            NotifyFilters.FileName |
            NotifyFilters.LastWrite |
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
        await m_lock.WaitAsync();
        try
        {
            string? line;
            while ((line = m_reader?.ReadLine()) != null)
            {
                if (!LogLine.TryParse(line, out var logLine) || logLine.TimeStamp < m_last_update)
                {
                    continue;
                }
                m_last_update = logLine.TimeStamp;

                try
                {
                    if (!await Parse(logLine))
                    {
                        Logger.Info($"Unhandled Log: {logLine.Message}");
                    }
                }
                catch (Exception exception)
                {
                    // A line we can't make sense of shouldn't be able to take the bot down with it
                    Logger.Error($"Failed to handle log line '{logLine.Message}': {exception.Message}");
                }
            }
        }
        finally
        {
            m_lock.Release();
        }
    }
    async void OnChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (m_reader == null)
            {
                await OpenReader(e.FullPath);
            }
            await Update();
        }
        catch (Exception exception)
        {
            // Nothing awaits an event handler, so anything escaping here would be unhandled
            Logger.Error($"Failed to read {e.FullPath}: {exception.Message}");
        }
    }

    async void OnCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            await OpenReader(e.FullPath);
        }
        catch (Exception exception)
        {
            Logger.Error($"Failed to open {e.FullPath}: {exception.Message}");
        }
    }

    /// <summary>
    /// Start reading a log file from the beginning, closing whatever we were reading before it.
    /// </summary>
    private async Task OpenReader(string path)
    {
        await m_lock.WaitAsync();
        try
        {
            m_reader?.Dispose();
            m_reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            Logger.Info($"Watching new log file: {path}");
        }
        finally
        {
            m_lock.Release();
        }
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

    public void Dispose()
    {
        m_watcher.Dispose();
        m_reader?.Dispose();
        m_lock.Dispose();
        GC.SuppressFinalize(this);
    }
}