using System;
using System.Collections.Generic;
using System.Text;
using NLog;

namespace DtronixPackage.Tests
{
    class NLogLogger : ILogger
    {
        private readonly Logger _logger;

        public NLogLogger(string className)
        {
            _logger = LogManager.GetLogger(className);
        }
        public void Log(LogEntry entry)
        {
            var logLevel = entry.Severity switch
            {
                LogEntryEventType.Trace => LogLevel.Trace,
                LogEntryEventType.Debug => LogLevel.Debug,
                LogEntryEventType.Info => LogLevel.Info,
                LogEntryEventType.Warn => LogLevel.Warn,
                LogEntryEventType.Error => LogLevel.Error,
                LogEntryEventType.Fatal => LogLevel.Fatal,
            };

            if(entry.Exception != null)
                _logger.Log(logLevel, entry.Exception, entry.Message);
            else
                _logger.Log(logLevel, entry.Message);
        }
    }
}
