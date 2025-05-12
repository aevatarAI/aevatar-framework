using System.Reflection;
using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core;

public abstract class StateProjectionGAgentBase<TProjectionState, TState, TStateLogEvent> :
    StateProjectionGAgentBase<TProjectionState, TState, TStateLogEvent, EventBase>
    where TProjectionState : StateBase, new()
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>;

public abstract class StateProjectionGAgentBase<TProjectionState, TState, TStateLogEvent, TEvent> :
    StateProjectionGAgentBase<TProjectionState, TState, TStateLogEvent, TEvent, ConfigurationBase>
    where TProjectionState : StateBase, new()
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase;

public abstract class StateProjectionGAgentBase<TProjectionState, TState, TStateLogEvent, TEvent, TConfiguration> :
    GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
    where TProjectionState : StateBase, new()
    where TState : StateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TConfiguration : ConfigurationBase
{
    private readonly List<StateBaseAsyncObserver> _stateObservers = [];
    private readonly List<StreamSubscriptionHandle<StateWrapper<TProjectionState>>> _subscription = [];

    protected override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        await UpdateStateLogEventObserverListAsync();
        await SubscribeStateProjectionStreamAsync();
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscription)
        {
            await subscription.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    protected abstract Task HandleStateAsync(StateWrapper<TProjectionState> projectionStateWrapper);

    protected int ProjectStateVersion { get; set; }

    private async Task UpdateStateLogEventObserverListAsync()
    {
        var stateHandlerMethods = GetStateHandlerMethods(GetType());
        foreach (var stateLogEventHandlerMethod in stateHandlerMethods)
        {
            var observer = new StateBaseAsyncObserver(async item =>
            {
                var version = (int)item.GetType().GetProperty(nameof(StateWrapper<TProjectionState>.Version))?.GetValue(item)!;
                if (ProjectStateVersion >= version)
                {
                    return;
                }

                ProjectStateVersion = version;
                var result = stateLogEventHandlerMethod.Invoke(this, [item]);
                await (Task)result!;
            })
            {
                GAgentGuid = this.GetPrimaryKey()
            };
            _stateObservers.Add(observer);
        }
    }

    private async Task SubscribeStateProjectionStreamAsync()
    {
        var projectionStream = GetStateProjectionStream();
        foreach (var stateObserver in _stateObservers)
        {
            var subscription = await projectionStream.SubscribeAsync(stateObserver);
            _subscription.Add(subscription);
        }
    }

    private IEnumerable<MethodInfo> GetStateHandlerMethods(Type type)
    {
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(IsStateHandlerMethod);
    }

    private bool IsStateHandlerMethod(MethodInfo methodInfo)
    {
        return methodInfo.GetParameters().Length == 1 &&
               (methodInfo.GetCustomAttribute<StateLogEventHandlerAttribute>() != null ||
                methodInfo.Name == AevatarGAgentConstants.StateHandlerDefaultMethodName) &&
               methodInfo.GetParameters()[0].ParameterType == typeof(StateWrapper<TProjectionState>);
    }

    private IAsyncStream<StateWrapper<TProjectionState>> GetStateProjectionStream()
    {
        var streamId = StreamId.Create(AevatarOptions!.StateProjectionStreamNamespace, typeof(StateWrapper<TProjectionState>).FullName!);
        return StreamProvider.GetStream<StateWrapper<TProjectionState>>(streamId);
    }
}