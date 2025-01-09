using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Orleans.Providers;

namespace Aevatar.Core;

[GAgent("baseWithInitialization")]
[StorageProvider(ProviderName = "PubSubStore")]
[LogConsistencyProvider(ProviderName = "LogStorage")]
public abstract class GAgentBase<TState, TStateLogEvent, TEvent, TInitializationDtoEvent> : GAgentBase<TState, TStateLogEvent, TEvent>
    where TState : StateBase, new()
    where TStateLogEvent: StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TInitializationDtoEvent : InitializationDtoEventBase
{
    protected GAgentBase(ILogger logger) : base(logger)
    {
    }
    
    public abstract Task InitializeAsync(TInitializationDtoEvent initializeDto);

    public override Task<Type?> GetInitializeDtoTypeAsync()
    {
        return Task.FromResult(typeof(TInitializationDtoEvent))!;
    }
}