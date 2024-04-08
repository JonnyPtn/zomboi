using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace zomboi
{
    internal class Server
    {
        private static string serverPath = "server";
        private static string StartPath { get 
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{serverPath}/StartServer64.bat";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{serverPath}/StartServer.Command";
            }
            else
            {
                return $"{serverPath}/start-server.sh";
            }
        }}
        private static readonly Process serverProcess = new();

        public static bool IsRunning { get { return serverProcess.StartInfo.FileName.Length > 0 && !serverProcess.HasExited; } }
        public static bool IsInstalled { get { return Directory.Exists(serverPath); } }
        public static bool IsCreated { get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Server/servertest.ini")); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Logs")); } }
        public static List<Player> players = new List<Player>();
        public static bool Start()
        {
            serverProcess.StartInfo.FileName = StartPath;
            serverProcess.StartInfo.RedirectStandardInput = true;
            serverProcess.Start();
            return IsRunning;
        }

        public static async Task Stop()
        {
            await serverProcess.StandardInput.WriteLineAsync("save");
            await serverProcess.StandardInput.WriteLineAsync("quit");
            await serverProcess.WaitForExitAsync();
        }

        private static async Task DownloadSteamCMD()
        {
            var downloadFile = "steamcmd.file";
            if (!File.Exists(downloadFile))
            {
                using (var client = new HttpClient())
                {
                    string uri = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd";

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        uri += ".zip";
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        uri += "_osx.tar.gz";
                    }
                    else
                    {
                        uri += "_linux.tar.gz";
                    }
                    using (var s = await client.GetStreamAsync(uri))
                    {
                        using (var fs = new FileStream(downloadFile, FileMode.CreateNew))
                        {
                            await s.CopyToAsync(fs);
                        }
                    }
                }
            }

            var unpackedPath = "steamcmd";
            if (!Directory.Exists(unpackedPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ZipFile.ExtractToDirectory(downloadFile, unpackedPath);
                }
                else
                {
                    Directory.CreateDirectory(unpackedPath);
                    using (var fs = new FileStream(downloadFile, FileMode.Open))
                    {
                        using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                        {
                            await TarFile.ExtractToDirectoryAsync(gz, unpackedPath, true);
                        }
                    }
                }
            }
        }

        public static async Task Install()
        {
            await DownloadSteamCMD();

            Process process = new();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process.StartInfo.FileName = "steamcmd/steamcmd.sh";
            }
            else
            {
                process.StartInfo.FileName = "steamcmd/steamcmd";
            }
            process.StartInfo.Arguments = "+runscript ../update_server.txt";
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.Start();
            await process.WaitForExitAsync();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // We don't want the batch file to wait for input, so remove any PAUSE
                var pause = "PAUSE";
                var file = File.ReadAllText(StartPath);
                file = file.Replace(pause, "");
                File.WriteAllText(StartPath, file);
            }
        }

        public static async Task Create(string password)
        {
            Start();
            await serverProcess.StandardInput.WriteLineAsync(password); // Set the password
            await serverProcess.StandardInput.WriteLineAsync(password); // Confirm the password
            await Stop();
        }

    }
}
