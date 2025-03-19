namespace Aevatar.Core.Interface;

public interface IGAgentEventBase
{
    Guid Id { get; set; }
    DateTime Ctime { get; set; }
}
