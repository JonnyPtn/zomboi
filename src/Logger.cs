using System.Diagnostics;

namespace zomboi
{
    internal class Logger
    {
        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ResetColor();
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        public static void Info(string message)
        {
            Console.WriteLine(message);
        }

        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Error.WriteLine(message);
            Console.ResetColor();
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

    }
}
