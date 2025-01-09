using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Tests.TestEvents;

[GenerateSerializer]
public class ReceiveMessageTestEvent : EventBase
{
    [Id(0)] public string? MessageId { get; set; }
    [Id(1)] public string? ChatId { get; set; }
    [Id(2)] public string? Message { get; set; }
    [Id(3)] public string? BotName { get; set; }
}