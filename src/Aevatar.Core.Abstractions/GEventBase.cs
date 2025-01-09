namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class StateLogEventBase
{
    [Id(0)] public virtual Guid Id { get; set; }
    [Id(1)] public DateTime Ctime { get; set; }
}

[GenerateSerializer]
public abstract class StateLogEvent<T> : StateLogEventBase
    where T:StateLogEvent<T>
{
    
}

public class TestStateLogEvent : StateLogEvent<TestStateLogEvent>
{
    
}