namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class StateLogEventBase
{
    [Id(0)] public virtual Guid Id { get; set; }
    [Id(1)] public DateTime Ctime { get; set; }
}

[GenerateSerializer]
public abstract class StateLogEventBase<T> : StateLogEventBase
    where T:StateLogEventBase<T>;