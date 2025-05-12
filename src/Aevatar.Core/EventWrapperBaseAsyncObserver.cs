using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace Aevatar.Core;

public class EventWrapperBaseAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly Func<EventWrapperBase, Task> _func;
    private readonly ILogger<EventWrapperBaseAsyncObserver> _logger;

    public string MethodName { get; set; }
    public string ParameterTypeName { get; set; }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func)
    {
        _func = func;
        _logger = null;
    }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func, ILogger<EventWrapperBaseAsyncObserver> logger)
    {
        _func = func;
        _logger = logger;
    }

    // Static factory method to create an instance with a logger from a service provider
    public static EventWrapperBaseAsyncObserver Create(Func<EventWrapperBase, Task> func, IServiceProvider serviceProvider, string methodName, string parameterTypeName)
    {
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<EventWrapperBaseAsyncObserver>();
        return new EventWrapperBaseAsyncObserver(func, logger)
        {
            MethodName = methodName,
            ParameterTypeName = parameterTypeName
        };
    }

    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        await _func(item);
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger?.LogError(ex, "Error invoking method {MethodName} with event type {EventType}", MethodName, ParameterTypeName);
        return Task.CompletedTask;
    }
}