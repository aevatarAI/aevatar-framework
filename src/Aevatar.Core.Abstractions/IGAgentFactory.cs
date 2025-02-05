namespace Aevatar.Core.Abstractions;

public interface IGAgentFactory
{
    Task<IGAgent> GetGAgentAsync(GrainId grainId, ConfigurationBase? configuration = null);

    Task<IGAgent> GetGAgentAsync(Guid primaryKey, string alias,
        string ns, ConfigurationBase? configuration = null);

    Task<IGAgent> GetGAgentAsync(string alias, string ns,
        ConfigurationBase? configuration = null);

    Task<IGAgent> GetGAgentAsync(Guid primaryKey, Type gAgentType, ConfigurationBase? configuration = null);

    Task<IGAgent> GetGAgentAsync(Type gAgentType, ConfigurationBase? configuration = null);

    Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(Guid primaryKey,
        ConfigurationBase? configuration = null)
        where TGrainInterface : IGAgent;

    Task<TGrainInterface> GetGAgentAsync<TGrainInterface>(ConfigurationBase? configuration = null)
        where TGrainInterface : IGAgent;
}