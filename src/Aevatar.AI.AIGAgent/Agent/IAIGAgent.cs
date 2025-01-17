using System.Threading.Tasks;
using Aevatar.AI.Dtos;
using Aevatar.Core.Abstractions;

namespace Aevatar.AI.Agent;

public interface IAIGAgent : IGAgent
{
    Task<bool> InitializeAsync(InitializeDto dto);
}