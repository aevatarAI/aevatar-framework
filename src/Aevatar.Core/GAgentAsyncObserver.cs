using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core;

public class GAgentAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly IEnumerable<EventWrapperBaseAsyncObserver> _observers;
    private readonly string _grainId;

    public GAgentAsyncObserver(IEnumerable<EventWrapperBaseAsyncObserver> observers, string grainId)
    {
        _observers = observers;
        _grainId = grainId;
    }
    
    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        var eventType = (EventBase)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.Event))?.GetValue(item)!;
        
        using var scope = OpenTelemetryScope.Start(_grainId, eventType, token);

        try
        {
            // TODO: Maybe use RuleEngine to optimize this.
            var matchedObservers = _observers.Where(observer =>
                observer.ParameterTypeName == eventType.GetType().Name ||
                observer.ParameterTypeName == nameof(EventWrapperBase) ||
                observer.MethodName == AevatarGAgentConstants.ForwardEventMethodName ||
                observer.MethodName == AevatarGAgentConstants.ConfigDefaultMethodName).ToList();
            foreach (var observer in matchedObservers)
            {
                // TODO: add tracing for individual observer
                await observer.OnNextAsync(item);
            }            
        }
        catch (Exception ex)
        {
            scope.RecordException(ex);
            throw;
        }

    }

    public async Task OnCompletedAsync()
    {
        foreach (var observer in _observers)
        {
            await observer.OnCompletedAsync();
        }
    }

    public async Task OnErrorAsync(Exception ex)
    {
        foreach (var observer in _observers)
        {
            await observer.OnErrorAsync(ex);
        }
    }
}