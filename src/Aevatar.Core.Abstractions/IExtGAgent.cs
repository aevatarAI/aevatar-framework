namespace Aevatar.Core.Abstractions;

/// <summary>
/// Represents an extended GAgent which intends to boosts the performance of the current GAgent. 
/// It should be merged back to IGAgent after the performance is improved.
/// </summary>
public interface IExtGAgent : IGAgent
{

    /// <summary>
    /// Register many GAgents as the next level of the current GAgent.
    /// To compare with RegisterAsync, this method is more efficient via batch processing.
    /// </summary>
    /// <param name="gAgent"></param>
    /// <returns></returns>
    Task RegisterManyAsync(List<IGAgent> gAgents);

}