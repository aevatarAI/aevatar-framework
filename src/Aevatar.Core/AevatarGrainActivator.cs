using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aevatar.Core;

public sealed class AevatarGrainActivator : IGrainActivator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IGrainActivator _defaultActivator;

    public AevatarGrainActivator(IServiceProvider serviceProvider, Type grainClass)
    {
        _serviceProvider = serviceProvider;
        _defaultActivator = new DefaultGrainActivator(serviceProvider, grainClass);
    }

    public object CreateInstance(IGrainContext context)
    {
        var grain = _defaultActivator.CreateInstance(context);
        InjectLogger(grain);
        return grain;
    }

    public void InjectLogger(object grain)
    {
        var grainType = grain.GetType();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var logger = loggerFactory.CreateLogger(grainType) ?? NullLogger.Instance;

        var loggerProperty = grainType.GetProperty("Logger");
        if (loggerProperty != null && loggerProperty.CanWrite)
        {
            loggerProperty.SetValue(grain, logger);
        }
    }

    public ValueTask DisposeInstance(IGrainContext context, object instance)
    {
        _defaultActivator.DisposeInstance(context, instance);
        return ValueTask.CompletedTask;
    }
}