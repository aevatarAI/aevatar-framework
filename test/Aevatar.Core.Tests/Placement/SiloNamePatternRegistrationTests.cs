using System.Linq;
using Aevatar.Core.Placement;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Runtime.Placement;
using Shouldly;
using Xunit;
using Moq;

namespace Aevatar.Core.Tests.Placement
{
    public class SiloNamePatternRegistrationTests
    {
        [Fact]
        public void PlacementDirector_Registration_ShouldRegisterCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            
            // Register required services for SiloNamePatternPlacementDirector
            var siloStatusOracle = Mock.Of<ISiloStatusOracle>();
            services.AddSingleton(siloStatusOracle);
            
            // Register the director directly without the extension method
            services.AddSingleton<SiloNamePatternPlacementDirector>();

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var director = serviceProvider.GetService<SiloNamePatternPlacementDirector>();
            director.ShouldNotBeNull();
        }

        [Fact]
        public void PlacementStrategy_Registration_WithPattern_ShouldRegisterCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            const string testPattern = "TestPattern";
            
            // Register required services and create the pattern placement
            var siloStatusOracle = Mock.Of<ISiloStatusOracle>();
            services.AddSingleton(siloStatusOracle);
            
            // Register the director
            services.AddSingleton<SiloNamePatternPlacementDirector>();
            
            // Register the strategy with a specific pattern
            services.AddSingleton(SiloNamePatternPlacement.Create(testPattern));
            services.AddSingleton<PlacementStrategy>(sp => sp.GetRequiredService<SiloNamePatternPlacement>());

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var strategy = serviceProvider.GetService<SiloNamePatternPlacement>();
            strategy.ShouldNotBeNull();
            
            var property = typeof(SiloNamePatternPlacement).GetProperty("SiloNamePattern")!;
            property.GetValue(strategy).ShouldBe(testPattern);
            
            // Verify it's also registered as a PlacementStrategy
            var strategies = serviceProvider.GetServices<PlacementStrategy>().ToList();
            strategies.Count.ShouldBeGreaterThan(0);
            strategies.Any(s => s is SiloNamePatternPlacement).ShouldBeTrue();
        }
    }
} 