using Orleans.Streams;

namespace Aevatar.Core.Abstractions;

public interface IGAgent : IGrainWithGuidKey, IAsyncObserver<EventWrapperBase>
{
    /// <summary>
    /// Used for activating the agent manually.
    /// </summary>
    /// <returns></returns>
    Task ActivateAsync();

    /// <summary>
    /// Get GAgent description.
    /// </summary>
    /// <returns></returns>
    Task<string> GetDescriptionAsync();

    /// <summary>
    /// Register a GAgent as the next level of the current GAgent.
    /// </summary>
    /// <param name="gAgent"></param>
    /// <returns></returns>
    Task RegisterAsync(IGAgent gAgent);

    /// <summary>
    /// Register current GAgent as the next level of the provided GAgent.
    /// </summary>
    /// <param name="gAgent"></param>
    /// <returns></returns>
    Task SubscribeToAsync(IGAgent gAgent);

    /// <summary>
    /// Undo the registration.
    /// </summary>
    /// <param name="gAgent"></param>
    /// <returns></returns>
    Task UnregisterAsync(IGAgent gAgent);

    /// <summary>
    /// Get all subscribed events of current GAgent.
    /// </summary>
    /// <param name="includeBaseHandlers"></param>
    /// <returns></returns>
    Task<List<Type>?> GetAllSubscribedEventsAsync(bool includeBaseHandlers = false);

    /// <summary>
    /// Get subscriber list of current GAgent.
    /// </summary>
    /// <returns></returns>
    Task<List<GrainId>> GetChildrenAsync();

    /// <summary>
    /// Get the subscription of current GAgent.
    /// </summary>
    /// <returns></returns>
    Task<GrainId> GetParentAsync();

    Task<Type?> GetInitializeDtoTypeAsync();
}

public interface IStateGAgent<TState> : IGAgent
{
    Task<TState> GetStateAsync();
}