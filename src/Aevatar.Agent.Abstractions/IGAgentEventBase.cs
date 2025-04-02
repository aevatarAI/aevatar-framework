namespace Aevatar.Agent.Abstractions;

public interface IGAgentEventBase
{
    Guid Id { get; set; }
    DateTime Ctime { get; set; }
}
