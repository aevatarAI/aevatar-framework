namespace Aevatar.Core.Abstractions.Plugin;

[GenerateSerializer]
public class AddPluginGAgentDto
{
    [Id(0)] public required byte[] Code { get; set; } = [];
    [Id(1)] public required Guid TenantId { get; set; }
}