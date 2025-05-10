namespace Aevatar.Agent.Abstractions;

public interface IGAgentEventBase<T> : IGAgentEventBase
    where T : IGAgentEventBase<T>
{

}
