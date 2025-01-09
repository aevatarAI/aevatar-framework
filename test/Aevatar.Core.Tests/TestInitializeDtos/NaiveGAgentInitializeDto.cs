using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestInitializeDtos;

[GenerateSerializer]
public class NaiveGAgentInitializeDto : InitializationDtoEventBase
{
    [Id(0)] public string InitialGreeting { get; set; }
}