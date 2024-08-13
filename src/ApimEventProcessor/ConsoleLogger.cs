using System;

namespace ApimEventProcessor
{
    public enum LogLevel { Debug, Info, Warning, Error };

    public class ConsoleLogger : ILogger
    {
        private LogLevel _LogLevel;
        private readonly object _writerLock = new object();


        public ConsoleLogger(LogLevel logLevel = LogLevel.Info)
        {
            _LogLevel = logLevel;
        }
        public void LogDebug(string message, params object[] parameters)
        {
            if (_LogLevel > LogLevel.Debug) return;
            WriteLine(ConsoleColor.Green, message, parameters);
        }
     
        public void LogInfo(string message, params object[] parameters)
        {
            if (_LogLevel > LogLevel.Info) return;
            WriteLine(ConsoleColor.Yellow, message, parameters);
        }

        public void LogWarning(string message, params object[] parameters)
        {
            if (_LogLevel > LogLevel.Warning) return;
            WriteLine(ConsoleColor.Blue, message, parameters);
        }

        public void LogError(string message, params object[] parameters)
        {
            WriteLine(ConsoleColor.Magenta, message, parameters);
        }

        private void WriteLine(ConsoleColor color, string message, object[] parameters)
        {
            lock (_writerLock)
            {
                var currentColor = Console.ForegroundColor;
                try
                {
                    currentColor = Console.ForegroundColor;
                    Console.ForegroundColor = color;
                    Console.WriteLine(string.Format(message, parameters));
                }
                finally
                {
                    Console.ForegroundColor = currentColor;
                }
            }
        }

    }

    class LogLevelUtil
    {
        public static LogLevel getLevelFromString(String desiredLevel, LogLevel defaultLevel)
        {
            LogLevel l = defaultLevel;
            if (matchesStr(desiredLevel, "debug") || matchesStr(desiredLevel, "trace") )
                l = LogLevel.Debug;
            else if (matchesStr(desiredLevel, "information"))
                l = LogLevel.Info;
            else if (matchesStr(desiredLevel, "warning"))
                l = LogLevel.Warning;
            else if (matchesStr(desiredLevel, "error") || matchesStr(desiredLevel, "fatal"))
                l = LogLevel.Error;
            return l;
        }

        private static Boolean matchesStr(String desiredLevel, String match)
        {
            Boolean m = false;
            if (!string.IsNullOrEmpty(desiredLevel))
                desiredLevel = desiredLevel.Replace("\"", "").Replace("'", "").Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(desiredLevel))
                m = match.Trim().ToLower().StartsWith(desiredLevel);
            return m;
        }
    }
}