using Aevatar.Core.Abstractions;
using System.Collections.Generic;

namespace Aevatar.Core;

/// <summary>
/// Event for publishing exceptions that occur during GAgent event handling.
/// Contains detailed information about the exception and its context.
/// </summary>
[GenerateSerializer]
public class GAgentExceptionEvent : EventBase
{
    /// <summary>
    /// The type of the exception.
    /// </summary>
    [Id(0)]
    public string ExceptionType { get; set; }

    /// <summary>
    /// The exception message.
    /// </summary>
    [Id(1)]
    public string ExceptionMessage { get; set; }

    /// <summary>
    /// Stack trace of the exception.
    /// </summary>
    [Id(2)]
    public string StackTrace { get; set; }

    /// <summary>
    /// The timestamp when the exception occurred.
    /// </summary>
    [Id(3)]
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// ID of the grain where the exception occurred.
    /// </summary>
    [Id(4)]
    public GrainId SourceGrainId { get; set; }

    /// <summary>
    /// Method or handler where the exception occurred.
    /// </summary>
    [Id(5)]
    public string SourceMethod { get; set; }

    /// <summary>
    /// ID of the event being processed when the exception occurred (if applicable).
    /// </summary>
    [Id(6)]
    public string EventId { get; set; }

    /// <summary>
    /// Type of the event being processed when the exception occurred (if applicable).
    /// </summary>
    [Id(7)]
    public string EventType { get; set; }

    /// <summary>
    /// Additional context information that might be useful for diagnosing the exception.
    /// </summary>
    [Id(8)]
    public Dictionary<string, string> ContextData { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Creates a new instance of GAgentExceptionEvent.
    /// </summary>
    public GAgentExceptionEvent()
    {
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Creates a new instance of GAgentExceptionEvent from an Exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="sourceGrainId">ID of the grain where the exception occurred.</param>
    /// <param name="sourceMethod">Method or handler where the exception occurred.</param>
    /// <param name="eventId">ID of the event being processed when the exception occurred (if applicable).</param>
    /// <param name="eventType">Type of the event being processed when the exception occurred (if applicable).</param>
    /// <returns>A new GAgentExceptionEvent with information from the exception.</returns>
    public static GAgentExceptionEvent FromException(
        Exception exception,
        GrainId sourceGrainId,
        string sourceMethod = "",
        string eventId = "",
        string eventType = "")
    {
        return new GAgentExceptionEvent
        {
            ExceptionType = exception.GetType().FullName,
            ExceptionMessage = exception.Message,
            StackTrace = exception.StackTrace ?? "",
            SourceGrainId = sourceGrainId,
            SourceMethod = sourceMethod,
            EventId = eventId,
            EventType = eventType,
            // Add common data from inner exceptions if available
            ContextData = ExtractContextData(exception)
        };
    }

    /// <summary>
    /// Extracts relevant context data from an exception and its inner exceptions.
    /// </summary>
    private static Dictionary<string, string> ExtractContextData(Exception exception)
    {
        var data = new Dictionary<string, string>();
        
        // Add data from the exception
        if (exception.Data.Count > 0)
        {
            foreach (var key in exception.Data.Keys)
            {
                if (key != null && exception.Data[key] != null)
                {
                    data[$"ExceptionData.{key}"] = exception.Data[key].ToString();
                }
            }
        }
        
        // Add inner exception details
        var innerException = exception.InnerException;
        var innerLevel = 1;
        
        while (innerException != null)
        {
            data[$"InnerException.{innerLevel}.Type"] = innerException.GetType().FullName;
            data[$"InnerException.{innerLevel}.Message"] = innerException.Message;
            
            innerException = innerException.InnerException;
            innerLevel++;
        }
        
        return data;
    }
} 