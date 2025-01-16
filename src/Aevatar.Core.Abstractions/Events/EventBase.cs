// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class EventBase
{
    [Id(0)] public Guid? CorrelationId { get; set; }
    [Id(1)] public GrainId PublisherGrainId { get; set; }
}