using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core;

public class GAgentAsyncObserver : IAsyncObserver<EventWrapperBase>
{
    private readonly List<EventWrapperBaseAsyncObserver> _observers;

    public GAgentAsyncObserver(List<EventWrapperBaseAsyncObserver> observers)
    {
        _observers = observers;
    }
    
    public async Task OnNextAsync(EventWrapperBase item, StreamSequenceToken? token = null)
    {
        var eventType = (EventBase)item.GetType().GetProperty(nameof(EventWrapper<EventBase>.Event))?.GetValue(item)!;
        // TODO: Maybe use RuleEngine to optimize this.
        var matchedObservers = _observers.Where(observer =>
            observer.ParameterTypeName == eventType.GetType().Name ||
            observer.ParameterTypeName == nameof(EventWrapperBase) ||
            observer.MethodName == AevatarGAgentConstants.ForwardEventMethodName ||
            observer.MethodName == AevatarGAgentConstants.ConfigDefaultMethodName).ToList();
        foreach (var observer in matchedObservers)
        {
            await observer.OnNextAsync(item);
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