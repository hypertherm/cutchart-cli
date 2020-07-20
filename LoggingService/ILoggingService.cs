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
}