using System.Reflection;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Microsoft.Extensions.Logging;

namespace Aevatar.Plugins.GAgents;

[GenerateSerializer]
public class PluginCodeStorageGAgentState : StateBase
{
    [Id(0)] public byte[] Code { get; set; }
    [Id(1)] public Dictionary<string, string> Descriptions { get; set; } = new();
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

    private void UpdateDescriptions(byte[]? code)
    {
        // Clear previous descriptions
        State.Descriptions.Clear();

        // Basic validation: check for empty or suspicious code
        if (code == null || code.Length < 1024) // Arbitrary small size threshold
        {
            Logger.LogWarning($"[WARN] Plugin code is null or suspiciously small (length: {code?.Length ?? 0}).");
            return;
        }

        Assembly? assembly;
        try
        {
            assembly = Assembly.Load(code);
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ERROR] Failed to load plugin assembly: {ex.Message}");
            return;
        }

        Type[] gAgentTypes;
        try
        {
            gAgentTypes = assembly.GetTypesIgnoringLoadException()
                .Where(type => typeof(IGAgent).IsAssignableFrom(type) && type is { IsInterface: false, IsAbstract: false })
                .ToArray();
        }
        catch (Exception ex)
        {
            Logger.LogError($"[ERROR] Failed to enumerate types in plugin assembly: {ex.Message}");
            return;
        }

        foreach (var gAgentType in gAgentTypes)
        {
            object? instance = null;
            try
            {
                instance = Activator.CreateInstance(gAgentType);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[WARN] Could not instantiate {gAgentType.FullName}: {ex.Message}");
                continue;
            }

            try
            {
                var getDescriptionMethod = gAgentType.GetMethod(nameof(IGAgent.GetDescriptionAsync));
                if (getDescriptionMethod != null)
                {
                    var task = (Task<string>?)getDescriptionMethod.Invoke(instance, null);
                    var description = task?.GetAwaiter().GetResult() ?? "(No description)";
                    // Use AssemblyQualifiedName as key for serialization safety
                    State.Descriptions[gAgentType.AssemblyQualifiedName ?? gAgentType.FullName ?? "UnknownType"] = description;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[WARN] Failed to get description for {gAgentType.FullName}: {ex.Message}");
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