// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class AddChildGEvent : StateLogEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}


[GenerateSerializer]
public class AddChildGEvent<TGEvent> : StateLogEvent<TGEvent>
where TGEvent : StateLogEvent<TGEvent>
{
    [Id(0)] public GrainId Child { get; set; }
}