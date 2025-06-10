using System;
using System.Linq;
using Aevatar.Core.Placement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Metadata;
using Orleans.Runtime;
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
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            services.AddSingleton(mockSiloStatusOracle.Object);
            
            var mockLogger = new Mock<ILogger<SiloNamePatternPlacementDirector>>();
            services.AddSingleton(mockLogger.Object);
            
            // Register IClusterManifestProvider for GrainPropertiesResolver
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            services.AddSingleton(mockClusterManifestProvider.Object);
            
            // Register GrainPropertiesResolver with the manifest provider
            services.AddSingleton<GrainPropertiesResolver>();
            
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
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            services.AddSingleton(mockSiloStatusOracle.Object);
            
            // Register IClusterManifestProvider for GrainPropertiesResolver
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            services.AddSingleton(mockClusterManifestProvider.Object);
            
            // Register GrainPropertiesResolver with the manifest provider
            services.AddSingleton<GrainPropertiesResolver>();
            
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
        
        [Fact]
        public void PlacementDirector_Constructor_RequiresAllParameters()
        {
            // Arrange
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>().Object;
            
            // Create a mock ClusterManifestProvider for GrainPropertiesResolver
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>().Object;
            var grainPropertiesResolver = new GrainPropertiesResolver(mockClusterManifestProvider);
            
            var logger = new Mock<ILogger<SiloNamePatternPlacementDirector>>().Object;
            
            // Act - Verify creating with valid parameters succeeds
            var director = new SiloNamePatternPlacementDirector(mockSiloStatusOracle, grainPropertiesResolver,logger);
            
            // Assert
            director.ShouldNotBeNull();
            
            // Verify constructor throws with null parameters
            Should.NotThrow(() => new SiloNamePatternPlacementDirector(mockSiloStatusOracle, grainPropertiesResolver,logger));
            
            // Check that constructor actually does throw for null parameters
            Should.Throw<ArgumentNullException>(() => 
                new SiloNamePatternPlacementDirector(null!, grainPropertiesResolver,logger));
                
            Should.Throw<ArgumentNullException>(() => 
                new SiloNamePatternPlacementDirector(mockSiloStatusOracle, null!,logger));
        }
    }
} 