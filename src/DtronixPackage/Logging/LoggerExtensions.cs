using System;
using System.Diagnostics;

namespace DtronixPackage.Logging;

public static class LoggerExtensions
{
    private const string NoMessage = "No message from exception";
    // Trace
    public static void Trace(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, exception?.Message ?? NoMessage, exception));
    }  

    public static void Trace(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, message));
    }  

    public static void Trace(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, message, exception));
    }

    // Debug
    public static void Debug(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, exception?.Message ?? NoMessage, exception));
    } 
        
    public static void Debug(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, message));
    }

    public static void Debug(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, message, exception));
    }

    // Info
    public static void Info(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Info, exception?.Message ?? NoMessage, exception));
    } 
        
    public static void Info(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Info, message));
    }  

    public static void Info(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Info, message, exception));
    }  

    // Warn
    public static void Warn(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Warn, exception?.Message ?? NoMessage, exception));
    }  
        
    public static void Warn(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Warn, message));
    }  

    public static void Warn(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Warn, message, exception));
    }

    // Error
    public static void Error(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Error, exception?.Message ?? NoMessage, exception));
    }  
                
    public static void Error(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Error, message));
    }  

    public static void Error(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Error, message, exception));
    }

    // Fatal
    public static void Fatal(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Fatal, exception?.Message ?? NoMessage, exception));
    } 
        
    public static void Fatal(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Fatal, message));
    }  

    public static void Fatal(this ILogger logger, Exception? exception, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Fatal, message, exception));
    }

    [Conditional("DEBUG")]
    public static void ConditionalDebug(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, exception?.Message ?? NoMessage, exception));
    }  
        
    [Conditional("DEBUG")]
    public static void ConditionalDebug(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, message));
    } 

    [Conditional("DEBUG")]
    public static void ConditionalDebug(this ILogger logger, string message, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Debug, message, exception));
    }       
        
    [Conditional("DEBUG")]
    public static void ConditionalTrace(this ILogger logger, string message) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, message));
    } 
        
    [Conditional("DEBUG")]
    public static void ConditionalTrace(this ILogger logger, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, exception?.Message ?? NoMessage, exception));
    }

    [Conditional("DEBUG")]
    public static void ConditionalTrace(this ILogger logger, string message, Exception? exception) {
        logger.Log(new LogEntry(LogEntryEventType.Trace, message, exception));
    }
}