using System;
using System.IO;
using static Hypertherm.Logging.LoggingService;

namespace Hypertherm.Logging
{
    public interface ILoggingService
    {
        void ClearLog();
        void DumpLog();
        bool isError();
        void Log(string message, MessageType type);
    }

    public class LoggingService : ILoggingService
    {
        private string _filename;
        private MessageType _logggingLevel;
        private bool _error;

        public enum MessageType
        {
            DisplayInfo = 0,
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
            if (type == MessageType.Error)
            {
                _error = true;
            }

            if (type <= _logggingLevel)
            {
                Console.WriteLine(message);
            }

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