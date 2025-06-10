namespace Aevatar.Core.Abstractions.Plugin;

[GenerateSerializer]
public class PluginsInformation
{
    /// <summary>
    /// PluginCodeId -> Description.
    /// </summary>
    [Id(0)]
    public Dictionary<Guid, Dictionary<Type, string>> Value { get; set; } = new();
}