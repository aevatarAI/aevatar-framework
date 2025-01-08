namespace Aevatar.Core.Abstractions;

public interface IGAgent : IGrainWithGuidKey
{
    /// <summary>
    /// Used for activating the agent manually, mostly used for testing
    /// </summary>
    /// <returns></returns>
    Task ActivateAsync();

    //probably need a function to get event description
    //Task<string> GetEventDescriptionAsync();

    //Function to get agent description
    Task<string> GetDescriptionAsync();
    Task RegisterAsync(IGAgent gAgent);
    Task SubscribeToAsync(IGAgent gAgent);
    Task UnregisterAsync(IGAgent gAgent);
    Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false);
    Task<List<GrainId>> GetChildrenAsync();
    Task<GrainId> GetParentAsync();
}

public interface IStateGAgent<TState> : IGAgent
{
    Task<TState> GetStateAsync();
}