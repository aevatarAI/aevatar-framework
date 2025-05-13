using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime.Placement;

using Aevatar.Core.Placement;
using MessagingGAgent.Grains;

namespace Aevatar.Examples
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                // Call the ConfigureExampleAsync method as the entry point
                await SiloNamePatternPlacementExamples.ConfigureExampleAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
    }    

    /// <summary>
    /// Examples demonstrating how to use the SiloNamePatternPlacement strategy.
    /// </summary>
    public static class SiloNamePatternPlacementExamples
    {
        /// <summary>
        /// Example showing how to configure a silo host to use the SiloNamePatternPlacement strategy.
        /// </summary>
        public static async Task ConfigureExampleAsync()
        {
            // Configure and start a silo with the SiloNamePatternPlacement strategy
            IHostBuilder builder = Host.CreateDefaultBuilder()
                .UseOrleansClient(client =>
                {
                    client.UseLocalhostClustering();
                })
                .ConfigureLogging(logging => logging.AddConsole())
                .UseConsoleLifetime();

            using IHost host = builder.Build();

            await host.StartAsync();
            // Create a client to interact with the silo
            var client = host.Services.GetRequiredService<IClusterClient>();

            await CallWithPlacementHintAsync(client.GetGrain<ISpecializedGrain>(Guid.NewGuid()));
            // Wait for a while to allow the grain call to complete
            await Task.Delay(1000);
            await host.StopAsync();
        }

        /// <summary>
        /// Example demonstrating how to programmatically set a placement hint for a grain call.
        /// </summary>
        /// <param name="grain">The grain to call.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task CallWithPlacementHintAsync(ISpecializedGrain grain)
        {
            // Set the placement hint to target silos whose names contain "Analytics"
            // This will match silos like "AnalyticsSilo-01", "AnalyticsSilo-02", etc.
            RequestContext.Set(SiloNamePatternPlacement.SiloNamePatternPropertyKey, "Analytics");
            
            // This call will be routed to a silo whose name begins with "Analytics" if available
            await grain.DoSomethingAsync();
            
            // Clear the placement hint after the call
            RequestContext.Remove(SiloNamePatternPlacement.SiloNamePatternPropertyKey);
        }
    }
} 