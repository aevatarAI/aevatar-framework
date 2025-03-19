namespace Aevatar.Core.Interface;

public interface IGAgentEventBase<T> : IGAgentEventBase
    where T : IGAgentEventBase<T>
{

}
