using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;

namespace zomboi
{
    internal class Server
    {
        private static string serverPath = "server";
        private static string serverStartPath = $"{serverPath}/StartServer64.bat";
        private static readonly Process serverProcess = new();

        public static bool IsRunning { get { return serverProcess.StartInfo.FileName.Length > 0 && !serverProcess.HasExited; } }
        public static bool IsInstalled { get { return Directory.Exists(serverPath); } }
        public static bool IsCreated { get { return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Server/servertest.ini")); } }
        public static string LogFolderPath { get { return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Zomboid/Logs")); } }
        public static List<Player> players = new List<Player>();
        public static bool Start()
        {
            serverProcess.StartInfo.FileName = serverStartPath;
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

        public static async Task Install()
        {
            var zipPath = "steamcmd.zip";
            if (!File.Exists(zipPath))
            {
                using (var client = new WebClient())
                {
                    client.DownloadFile("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip", zipPath);
                }
            }

            var unpackedPath = "steamcmd";
            if (!Directory.Exists(unpackedPath))
            {
                ZipFile.ExtractToDirectory(zipPath, unpackedPath);
            }

            Process process = new();
            process.StartInfo.FileName = "steamcmd/steamcmd";
            process.StartInfo.Arguments = "+runscript ../update_server.txt";
            process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
            process.Start();
            await process.WaitForExitAsync();

            // We don't want the batch file to wait for input, so remove any PAUSE
            var pause = "PAUSE";
            var file = File.ReadAllText(serverStartPath);
            file = file.Replace(pause, "");
            File.WriteAllText(serverStartPath, file);
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
