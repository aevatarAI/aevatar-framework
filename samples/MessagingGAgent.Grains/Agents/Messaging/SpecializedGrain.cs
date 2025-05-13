using Aevatar.Core.Placement;

namespace MessagingGAgent.Grains;
/// <summary>
/// Example of a grain class that uses the SiloNamePatternPlacement attribute to specify placement.
/// </summary>
[SiloNamePatternPlacement("Analytics")]
public class SpecializedGrain : Orleans.Grain, ISpecializedGrain
{
    public Task DoSomethingAsync()
    {
        // This grain will be activated on a silo whose name begins with "Analytics" if available
        // For example, it will match "AnalyticsSilo-01", "DataAnalytics", etc.
        Console.WriteLine($"Grain activated on silo: {this.RuntimeIdentity}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Interface for the specialized grain example.
/// </summary>
public interface ISpecializedGrain : Orleans.IGrainWithGuidKey
{
    Task DoSomethingAsync();
}