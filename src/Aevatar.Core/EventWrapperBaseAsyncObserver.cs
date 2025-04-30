using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core;

public class EventWrapperBaseAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly Func<EventWrapperBase, Task> _func;
    public ILogger Logger { get; set; } = NullLogger.Instance;

    public string MethodName { get; set; }
    public string ParameterTypeName { get; set; }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func)
    {
        _func = func;
        Logger = NullLogger.Instance;
    }

    public EventWrapperBaseAsyncObserver(Func<EventWrapperBase, Task> func, ILogger logger)
    {
        _func = func;
        Logger = logger ?? NullLogger.Instance;
    }

    // Static factory method to create an instance with a logger from a service provider
    public static EventWrapperBaseAsyncObserver Create(Func<EventWrapperBase, Task> func, IServiceProvider serviceProvider, string methodName, string parameterTypeName)
    {
        var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
        ILogger logger = loggerFactory != null ? loggerFactory.CreateLogger<EventWrapperBaseAsyncObserver>() : NullLogger.Instance;
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
        Logger?.LogError(ex, "Error invoking method {MethodName} with event type {EventType}", MethodName, ParameterTypeName);
        return Task.CompletedTask;
    }
}