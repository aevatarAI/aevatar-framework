// ReSharper disable once CheckNamespace
namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class AddChildGEvent : GEventBase
{
    [Id(0)] public GrainId Child { get; set; }
}


[GenerateSerializer]
public class AddChildGEvent<TGEvent> : GEventBase<TGEvent>
where TGEvent : GEventBase<TGEvent>
{
    [Id(0)] public GrainId Child { get; set; }
}