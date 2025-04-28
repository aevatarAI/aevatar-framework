using System.Threading.Tasks;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Streams;

namespace Aevatar.Core;

/// <summary>
/// Base class for asynchronous observers that process events from event wrappers
/// </summary>
public class EventWrapperBaseAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly Func<EventWrapperBase, Task> _func;
    private readonly ILogger<EventWrapperBaseAsyncObserver> _logger;
    private readonly GAgentBase<dynamic, dynamic, dynamic, dynamic>? _gagentBase;

    public string MethodName { get; set; }
    public string ParameterTypeName { get; set; }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func)
    {
        _func = func;
        _logger = null;
        MethodName = string.Empty;
        ParameterTypeName = string.Empty;
    }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func, ILogger<EventWrapperBaseAsyncObserver> logger)
    {
        _func = func;
        _logger = logger;
        MethodName = string.Empty;
        ParameterTypeName = string.Empty;
    }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func, GAgentBase<dynamic, dynamic, dynamic, dynamic> gagentBase)
    {
        _func = func;
        _logger = null;
        _gagentBase = gagentBase;
        MethodName = string.Empty;
        ParameterTypeName = string.Empty;
    }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func, ILogger<EventWrapperBaseAsyncObserver> logger, GAgentBase<dynamic, dynamic, dynamic, dynamic> gagentBase)
    {
        _func = func;
        _logger = logger;
        _gagentBase = gagentBase;
        MethodName = string.Empty;
        ParameterTypeName = string.Empty;
    }

    // Static factory method to create an instance with a logger from a service provider
    public static EventWrapperBaseAsyncObserver Create(Func<EventWrapperBase, Task> func, IServiceProvider serviceProvider, string methodName, string parameterTypeName, GAgentBase<dynamic, dynamic, dynamic, dynamic>? gagentBase = null)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<EventWrapperBaseAsyncObserver>();
        
        var observer = gagentBase != null
            ? new EventWrapperBaseAsyncObserver(func, logger, gagentBase)
            : new EventWrapperBaseAsyncObserver(func, logger);
            
        observer.MethodName = methodName;
        observer.ParameterTypeName = parameterTypeName;
        
        return observer;
    }

    /// <summary>
    /// Called when a new event arrives
    /// </summary>
    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        try
        {
            if (_gagentBase != null)
            {
                // Try to use exception publishing if GAgent base is available
                try
                {
                    await _gagentBase.InvokeWithExceptionPublishingAsync(
                        async () => await _func(item),
                        $"{MethodName}({ParameterTypeName})"
                    );
                }
                catch (Exception)
                {
                    // If InvokeWithExceptionPublishingAsync fails for any reason, fall back to direct invocation
                    await _func(item);
                }
            }
            else
            {
                // Direct invocation without exception publishing
                await _func(item);
            }
        }
        catch (Exception ex)
        {
            // Log the exception
            _logger?.LogError(ex, "Error handling event {EventType} in method {MethodName}", 
                ParameterTypeName, MethodName);
            
            // Re-throw the exception
            throw;
        }
    }

    /// <summary>
    /// Called when the stream completes
    /// </summary>
    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when an error occurs in the stream
    /// </summary>
    public Task OnErrorAsync(Exception ex)
    {
        return Task.CompletedTask;
    }
}