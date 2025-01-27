using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Plugins;

[GenerateSerializer]
public class PluginCodeStorageGAgentState : StateBase
{
    [Id(0)] public byte[] Code { get; set; }
}

[GenerateSerializer]
public class PluginCodeStorageStateLogEvent : StateLogEventBase<PluginCodeStorageStateLogEvent>
{
    
}

[GenerateSerializer]
public class PluginCodeStorageConfiguration : ConfigurationBase
{
    [Id(0)] public byte[] Code { get; set; }
}

public interface IPluginCodeStorageGAgent : IStateGAgent<PluginCodeStorageGAgentState>
{
    Task<byte[]> GetPluginCodeAsync();
    Task UpdatePluginCodeAsync(byte[] code);
}


[GAgent("pluginCodeStorage")]
public class PluginCodeStorageGAgent(ILogger<PluginCodeStorageGAgent> logger)
    : GAgentBase<PluginCodeStorageGAgentState, PluginCodeStorageStateLogEvent, EventBase,
        PluginCodeStorageConfiguration>(logger), IPluginCodeStorageGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("This GAgent is used to store plugin's code.");
    }

    protected override void GAgentTransitionState(PluginCodeStorageGAgentState state,
        StateLogEventBase<PluginCodeStorageStateLogEvent> @event)
    {
        switch (@event)
        {
            case SetPluginCodeStateLogEvent setPluginCodeStateLogEvent:
                State.Code = setPluginCodeStateLogEvent.Code;
                break;
            case UpdatePluginCodeStateLogEvent updatePluginCodeStateLogEvent:
                State.Code = updatePluginCodeStateLogEvent.Code;
                break;
        }
    }

    [GenerateSerializer]
    public class SetPluginCodeStateLogEvent : PluginCodeStorageStateLogEvent
    {
        [Id(0)] public byte[] Code { get; set; }
    }
    
    [GenerateSerializer]
    public class UpdatePluginCodeStateLogEvent : PluginCodeStorageStateLogEvent
    {
        [Id(0)] public required byte[] Code { get; set; }
    }

    protected override async Task PerformConfigAsync(PluginCodeStorageConfiguration configuration)
    {
        RaiseEvent(new SetPluginCodeStateLogEvent
        {
            Code = configuration.Code
        });
        await ConfirmEvents();
    }

    public Task<byte[]> GetPluginCodeAsync()
    {
        return Task.FromResult(State.Code);
    }

    public async Task UpdatePluginCodeAsync(byte[] code)
    {
        RaiseEvent(new UpdatePluginCodeStateLogEvent
        {
            Code = code
        });
        await ConfirmEvents();
    }
}