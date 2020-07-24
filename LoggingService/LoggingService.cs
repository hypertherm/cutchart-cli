using System;
using System.IO;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.Logging
{
    public class LoggingService : ILoggingService
    {
        private string _filename;
        private MessageType _logggingLevel;
        private bool _error;

        public enum MessageType
        {
            DisplayText = 0,
            DisplayData,
            Warning,
            Error,
            DebugInfo
        }

        public LoggingService(MessageType logggingLevel = MessageType.Error)
        {
            _filename = "./cc-cli.log";
            _logggingLevel = logggingLevel;
            _error = false;
        }

        public void Log(string message, MessageType type)
        {
            ConsoleColor defaultConsoleColor = Console.ForegroundColor;
            
            if (type == MessageType.Error)
            {
                _error = true;
                message = $"{message}";
                Console.ForegroundColor = ConsoleColor.Red;
            }
            else if (type == MessageType.Warning)
            {
                message = $"{message}";
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            else if (type == MessageType.DebugInfo)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
            }
            else if (type == MessageType.DisplayData)
            {
                Console.ForegroundColor = ConsoleColor.Green;
            }

            if (type <= _logggingLevel)
            {
                Console.WriteLine(message);
            }

            Console.ForegroundColor = defaultConsoleColor;

            using (StreamWriter w = File.AppendText(_filename))
            {
                w.Write($"{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()} - ");
                w.WriteLine($"  {type.ToString()}: {message}");
            }
        }

        public void DumpLog()
        {
            using (StreamReader r = File.OpenText(_filename))
            {
                string line;
                while ((line = r.ReadLine()) != null)
                {
                    Console.WriteLine(line);
                }
            }
        }

        public void ClearLog()
        {
            File.Delete(_filename);
        }

        public bool isError()
        {
            return _error;
        }
    }
}