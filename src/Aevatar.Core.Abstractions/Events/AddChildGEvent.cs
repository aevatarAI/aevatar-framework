// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class AddChildStateLogEvent : StateLogEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}


[GenerateSerializer]
public class AddChildStateLogEvent<TStateLogEvent> : StateLogEventBase<TStateLogEvent>
where TStateLogEvent : StateLogEventBase<TStateLogEvent>
{
    [Id(0)] public GrainId Child { get; set; }
}