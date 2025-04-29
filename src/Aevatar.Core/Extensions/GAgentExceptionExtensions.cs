using System.Runtime.CompilerServices;
using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Extensions;

/// <summary>
/// Provides exception handling extension methods for GAgentBase
/// </summary>
public static class GAgentExceptionExtensions
{
    /// <summary>
    /// Publishes an exception to Orleans Stream
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TStateLogEvent">StateLogEvent type</typeparam>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <typeparam name="TConfiguration">Configuration type</typeparam>
    /// <param name="gAgent">GAgentBase instance</param>
    /// <param name="exception">Exception to publish</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>Exception event ID</returns>
    public static Task<Guid> PublishExceptionAsync<TState, TStateLogEvent, TEvent, TConfiguration>(
        this GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration> gAgent,
        Exception exception,
        object? contextData = null,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
        where TState : StateBase, new()
        where TStateLogEvent : StateLogEventBase<TStateLogEvent>
        where TEvent : EventBase
        where TConfiguration : ConfigurationBase
    {
        return ((Grain)gAgent).PublishExceptionAsync(exception, contextData, callerMemberName, callerClassName);
    }

    /// <summary>
    /// Executes an operation, catches any exception and publishes it to Orleans Stream
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TStateLogEvent">StateLogEvent type</typeparam>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <typeparam name="TConfiguration">Configuration type</typeparam>
    /// <param name="gAgent">GAgentBase instance</param>
    /// <param name="action">Operation to execute</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="rethrowException">Whether to rethrow the exception, defaults to true</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>If an exception occurs, returns the exception event ID; otherwise returns Guid.Empty</returns>
    public static Task<Guid> CatchAndPublishExceptionAsync<TState, TStateLogEvent, TEvent, TConfiguration>(
        this GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration> gAgent,
        Func<Task> action,
        object? contextData = null,
        bool rethrowException = true,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
        where TState : StateBase, new()
        where TStateLogEvent : StateLogEventBase<TStateLogEvent>
        where TEvent : EventBase
        where TConfiguration : ConfigurationBase
    {
        return ((Grain)gAgent).CatchAndPublishExceptionAsync(action, contextData, rethrowException, callerMemberName, callerClassName);
    }

    /// <summary>
    /// Executes an operation, catches any exception and publishes it to Orleans Stream, returns the operation result
    /// </summary>
    /// <typeparam name="TState">State type</typeparam>
    /// <typeparam name="TStateLogEvent">StateLogEvent type</typeparam>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <typeparam name="TConfiguration">Configuration type</typeparam>
    /// <typeparam name="TResult">Operation result type</typeparam>
    /// <param name="gAgent">GAgentBase instance</param>
    /// <param name="func">Operation to execute</param>
    /// <param name="defaultValue">Default value to return if an exception occurs and is not rethrown</param>
    /// <param name="contextData">Context data, can be any serializable object</param>
    /// <param name="rethrowException">Whether to rethrow the exception, defaults to true</param>
    /// <param name="callerMemberName">Caller method name, auto-populated</param>
    /// <param name="callerClassName">Caller class name, auto-populated</param>
    /// <returns>If the operation succeeds, returns the operation result; if an exception occurs and is not rethrown, returns the default value</returns>
    public static Task<(TResult Result, Guid ExceptionId)> CatchAndPublishExceptionAsync<TState, TStateLogEvent, TEvent, TConfiguration, TResult>(
        this GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration> gAgent,
        Func<Task<TResult>> func,
        TResult defaultValue = default!,
        object? contextData = null,
        bool rethrowException = true,
        [CallerMemberName] string? callerMemberName = null,
        [CallerFilePath] string? callerClassName = null)
        where TState : StateBase, new()
        where TStateLogEvent : StateLogEventBase<TStateLogEvent>
        where TEvent : EventBase
        where TConfiguration : ConfigurationBase
    {
        return ((Grain)gAgent).CatchAndPublishExceptionAsync(func, defaultValue, contextData, rethrowException, callerMemberName, callerClassName);
    }
} 