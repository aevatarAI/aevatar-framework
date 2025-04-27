using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;

namespace Aevatar.Plugins.GAgents;

[GenerateSerializer]
public class PluginCodeStorageGAgentState : StateBase
{
    [Id(0)] public byte[] Code { get; set; }
    [Id(1)] public Dictionary<Type, string> Descriptions { get; set; } = new();
}

[GenerateSerializer]
public class PluginCodeStorageStateLogEvent : StateLogEventBase<PluginCodeStorageStateLogEvent>;

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

[GAgent]
public class PluginCodeStorageGAgent
    : GAgentBase<PluginCodeStorageGAgentState, PluginCodeStorageStateLogEvent, EventBase,
        PluginCodeStorageConfiguration>, IPluginCodeStorageGAgent
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
                UpdateDescriptions(setPluginCodeStateLogEvent.Code);
                break;
            case UpdatePluginCodeStateLogEvent updatePluginCodeStateLogEvent:
                State.Code = updatePluginCodeStateLogEvent.Code;
                UpdateDescriptions(updatePluginCodeStateLogEvent.Code);
                break;
        }
    }

    private void UpdateDescriptions(byte[] code)
    {
        // Load the assembly from the binary code
        var assembly = Assembly.Load(code);

        // Find all types implementing IGAgent
        var gAgentTypes = assembly.GetTypes()
            .Where(type => typeof(IGAgent).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false });

        foreach (var gAgentType in gAgentTypes)
        {
            // Create an instance of the type
            var instance = Activator.CreateInstance(gAgentType);

            // Invoke GetDescriptionAsync using reflection
            var getDescriptionMethod = gAgentType.GetMethod(nameof(IGAgent.GetDescriptionAsync));
            if (getDescriptionMethod != null)
            {
                var task = (Task<string>)getDescriptionMethod.Invoke(instance, null)!;
                var description = task.GetAwaiter().GetResult();

                // Update the State.Descriptions dictionary
                State.Descriptions[gAgentType] = description;
            }
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