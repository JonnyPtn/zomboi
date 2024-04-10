using System.Diagnostics;
using zomboi;

abstract public class LogFileListener
{
    private readonly FileSystemWatcher m_watcher;
    private StreamReader? m_reader;
    private StreamWriter? m_writer;
    private DateTime m_last_update;
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
    async void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.Name == null)
        {
            Logger.Error("Received a file change event without a file name");
            return;
        }

        if (m_reader == null || m_writer == null)
        {
            Logger.Error($"File changed but reader/writer isn't open: {e.Name}");
            return;
        }

        var line = m_reader.ReadLine();
        while(line != null)
        {
            LogLine logLine = new(line);
            if (logLine.TimeStamp > m_last_update)
            {
                m_last_update = logLine.TimeStamp;

                if (!await Parse(logLine))
                {
                    await m_writer.WriteLineAsync(line);
                    if (Debugger.IsAttached)
                    {
                        await m_writer.FlushAsync();
                    }
                }
            }
            line = m_reader.ReadLine();
        }
    }
    void OnCreated(object sender, FileSystemEventArgs e)
    {
        var fileStream = new FileStream(Path.Combine(Server.LogFolderPath, e.FullPath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        m_reader = new StreamReader(fileStream);
        m_writer = new StreamWriter(new FileStream($"logs/{m_watcher.Filter}", FileMode.Create));
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