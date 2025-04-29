using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Runtime;
using Orleans.Streams;

namespace Aevatar.Core.Extensions;

/// <summary>
/// Provides extension methods for publishing exceptions to Orleans Stream
/// </summary>
public static class ExceptionPublisherExtensions
{
    /// <summary>
    /// Publishes an exception to Orleans Stream
    /// </summary>
    /// <param name="grain">Orleans Grain instance</param>
    /// <param name="exception">Exception to publish</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>Exception event ID</returns>
    public static async Task<Guid> PublishExceptionAsync(
        this Grain grain,
        Exception exception,
        object? contextData = null,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
    {
        try
        {
            var grainId = grain.GetGrainId();
            var context = grain.GrainContext;
            var options = context.ActivationServices.GetRequiredService<IOptions<AevatarOptions>>().Value;
            var streamProvider = grain.GetStreamProvider(AevatarCoreConstants.StreamProvider);
            
            var contextDataJson = "{}";
            if (contextData != null)
            {
                try
                {
                    contextDataJson = JsonSerializer.Serialize(contextData);
                }
                catch (Exception serializationEx)
                {
                    contextDataJson = $"{{\"error\":\"Failed to serialize context data: {serializationEx.Message}\"}}";
                }
            }
            
            // Extract class name
            string? className = null;
            if (!string.IsNullOrEmpty(callerClassName))
            {
                className = Path.GetFileNameWithoutExtension(callerClassName);
            }

            var exceptionEvent = new ExceptionEvent
            {
                GrainId = grainId,
                ExceptionMessage = exception.Message,
                ExceptionType = exception.GetType().FullName ?? "Unknown",
                StackTrace = exception.StackTrace ?? string.Empty,
                ContextData = contextDataJson,
                Timestamp = DateTime.UtcNow,
                MethodName = callerMemberName,
                ClassName = className
            };

            var streamNamespace = options.ExceptionStreamNamespace;
            var stream = streamProvider.GetStream<ExceptionEvent>(StreamId.Create(streamNamespace, options.ExceptionStreamKey));
            
            var eventId = Guid.NewGuid();
            exceptionEvent.CorrelationId = eventId;
            await stream.OnNextAsync(exceptionEvent);
            
            return eventId;
        }
        catch (Exception ex)
        {
            // If an error occurs while publishing the exception, log it but don't try to publish again (to avoid infinite loops)
            var context = grain.GrainContext;
            var logger = context.ActivationServices.GetService<ILogger<Grain>>();
            logger?.LogError(ex, "Failed to publish exception: {Message}", ex.Message);
            return Guid.Empty;
        }
    }
    
    /// <summary>
    /// Executes an operation, catches any exception and publishes it to Orleans Stream
    /// </summary>
    /// <param name="grain">Orleans Grain instance</param>
    /// <param name="action">Operation to execute</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="rethrowException">Whether to rethrow the exception, defaults to true</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>If an exception occurs, returns the exception event ID; otherwise returns Guid.Empty</returns>
    public static async Task<Guid> CatchAndPublishExceptionAsync(
        this Grain grain,
        Func<Task> action,
        object? contextData = null,
        bool rethrowException = true,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
    {
        try
        {
            await action();
            return Guid.Empty;
        }
        catch (Exception ex)
        {
            var eventId = await grain.PublishExceptionAsync(ex, contextData, callerMemberName, callerClassName);
            
            if (rethrowException)
            {
                throw;
            }
            
            return eventId;
        }
    }
    
    /// <summary>
    /// Executes an operation, catches any exception and publishes it to Orleans Stream, returns the operation result
    /// </summary>
    /// <typeparam name="T">Operation result type</typeparam>
    /// <param name="grain">Orleans Grain instance</param>
    /// <param name="func">Operation to execute</param>
    /// <param name="defaultValue">Default value to return if an exception occurs and is not rethrown</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="rethrowException">Whether to rethrow the exception, defaults to true</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>If the operation succeeds, returns the operation result; if an exception occurs and is not rethrown, returns the default value</returns>
    public static async Task<(T Result, Guid ExceptionId)> CatchAndPublishExceptionAsync<T>(
        this Grain grain,
        Func<Task<T>> func,
        T defaultValue = default!,
        object? contextData = null,
        bool rethrowException = true,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
    {
        try
        {
            var result = await func();
            return (result, Guid.Empty);
        }
        catch (Exception ex)
        {
            var eventId = await grain.PublishExceptionAsync(ex, contextData, callerMemberName, callerClassName);
            
            if (rethrowException)
            {
                throw;
            }
            
            return (defaultValue, eventId);
        }
    }
} 