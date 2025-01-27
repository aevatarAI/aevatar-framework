using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestInitializeDtos;

[GenerateSerializer]
public class NaiveGAgentConfiguration : ConfigurationBase
{
    [Id(0)] public string Greeting { get; set; } = string.Empty;
}