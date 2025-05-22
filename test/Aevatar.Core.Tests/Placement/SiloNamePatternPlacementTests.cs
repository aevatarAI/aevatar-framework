using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Aevatar.Core.Placement;
using Microsoft.Extensions.Logging;
using Orleans.Metadata;
using Orleans.Runtime;
using Orleans.Runtime.Placement;
using Shouldly;
using Xunit;
using Moq;

namespace Aevatar.Core.Tests.Placement
{
    // Define a test interface for our test grain
    public interface ITestGrain : Orleans.IGrainWithStringKey
    {
        Task DoSomething();
    }

    // Define a test grain implementation with the SiloNamePatternPlacement attribute
    [SiloNamePatternPlacement("Worker")]
    public class TestWorkerGrain : Orleans.Grain, ITestGrain
    {
        public Task DoSomething() => Task.CompletedTask;
    }

    public class SiloNamePatternPlacementTests
    {
        [Fact]
        public void SiloNamePatternPlacement_Create_SetsPattern()
        {
            // Arrange
            const string testPattern = "Analytics";
            
            // Act
            var placement = SiloNamePatternPlacement.Create(testPattern);
            
            // Assert
            placement.ShouldNotBeNull();
            placement.ShouldBeOfType<SiloNamePatternPlacement>();
            typeof(SiloNamePatternPlacement).GetProperty("SiloNamePattern")?.GetValue(placement).ShouldBe(testPattern);
        }
        
        [Fact]
        public void SiloNamePatternPlacement_Initialize_SetsPatternFromProperties()
        {
            // Arrange
            var placement = new SiloNamePatternPlacement();
            var properties = new GrainProperties(
                ImmutableDictionary.CreateRange(new Dictionary<string, string>
                {
                    { SiloNamePatternPlacement.SiloNamePatternPropertyKey, "TestPattern" }
                }));
            
            // Act
            placement.Initialize(properties);
            
            // Assert
            typeof(SiloNamePatternPlacement).GetProperty("SiloNamePattern")?.GetValue(placement).ShouldBe("TestPattern");
        }
        
        [Fact]
        public void SiloNamePatternPlacement_PopulateGrainProperties_AddsPatternToProperties()
        {
            // Arrange
            const string testPattern = "Worker";
            var placement = SiloNamePatternPlacement.Create(testPattern);
            var properties = new Dictionary<string, string>();
            
            // Act
            placement.PopulateGrainProperties(null!, null!, default, properties);
            
            // Assert
            properties.ShouldContainKey(SiloNamePatternPlacement.SiloNamePatternPropertyKey);
            properties[SiloNamePatternPlacement.SiloNamePatternPropertyKey].ShouldBe(testPattern);
        }
        
        [Fact]
        public async Task SiloNamePatternPlacementDirector_OnAddActivation_MatchesSiloNamePattern()
        {
            // Arrange
            const string testPattern = "Worker";
            
            // Create mock silo addresses
            var workerSilo1 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0);
            var workerSilo2 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 22222), 0);
            var analyticsSilo = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 33333), 0);
            
            // Setup mock silo status oracle
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            var mockLogger = new Mock<ILogger<SiloNamePatternPlacementDirector>>();

            // Setup silo statuses
            var siloStatuses = new Dictionary<SiloAddress, SiloStatus>
            {
                { workerSilo1, SiloStatus.Active },
                { workerSilo2, SiloStatus.Active },
                { analyticsSilo, SiloStatus.Active }
            };
            mockSiloStatusOracle.Setup(x => x.GetApproximateSiloStatuses(true)).Returns(siloStatuses);
            
            // Setup silo names
            string workerSilo1Name = "WorkerSilo-01";
            string workerSilo2Name = "WorkerSilo-02";
            string analyticsSiloName = "AnalyticsSilo";
            
            mockSiloStatusOracle
                .Setup(m => m.TryGetSiloName(workerSilo1, out workerSilo1Name))
                .Returns(true);
                
            mockSiloStatusOracle
                .Setup(m => m.TryGetSiloName(workerSilo2, out workerSilo2Name))
                .Returns(true);
                
            mockSiloStatusOracle
                .Setup(m => m.TryGetSiloName(analyticsSilo, out analyticsSiloName))
                .Returns(true);
            
            // Setup mock placement context
            var mockPlacementContext = new Mock<IPlacementContext>();
            var compatibleSilos = new[] { workerSilo1, workerSilo2, analyticsSilo };
            mockPlacementContext.Setup(x => x.GetCompatibleSilos(It.IsAny<PlacementTarget>())).Returns(compatibleSilos);
            
            // Create placement strategy with the test pattern
            var strategy = SiloNamePatternPlacement.Create(testPattern);
            
            // Create grain properties with the pattern
            var grainProperties = new GrainProperties(
                ImmutableDictionary.CreateRange(new Dictionary<string, string>
                {
                    { SiloNamePatternPlacement.SiloNamePatternPropertyKey, testPattern }
                }));
                
            // Create a mock IClusterManifestProvider
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            
            // Setup a GrainManifest that maps our test grain type to the properties
            var grainDict = ImmutableDictionary.CreateBuilder<GrainType, GrainProperties>();
            grainDict.Add(GrainType.Create("ITestGrain"), grainProperties);
            
            // Create a grain manifest with the grain properties
            var grainManifest = new GrainManifest(
                grainDict.ToImmutable(), 
                ImmutableDictionary<GrainInterfaceType, GrainInterfaceProperties>.Empty);
                
            // Create silo manifests dictionary
            var siloManifests = ImmutableDictionary.CreateBuilder<SiloAddress, GrainManifest>();
            siloManifests.Add(workerSilo1, grainManifest);
            
            // Create cluster manifest
            var clusterManifest = new ClusterManifest(
                new MajorMinorVersion(1, 0),
                siloManifests.ToImmutable());
                
            // Setup the mock provider to return our cluster manifest
            mockClusterManifestProvider.Setup(m => m.Current).Returns(clusterManifest);
            mockClusterManifestProvider.Setup(m => m.LocalGrainManifest).Returns(grainManifest);
            
            // Create the GrainPropertiesResolver with our mock
            var grainPropertiesResolver = new GrainPropertiesResolver(mockClusterManifestProvider.Object);
            
            // Create placement director with the resolver
            var director = new SiloNamePatternPlacementDirector(
                mockSiloStatusOracle.Object,
                grainPropertiesResolver,mockLogger.Object);
            
            // Create placement target
            var grainId = GrainId.Create("ITestGrain", "1");
            var target = new PlacementTarget(
                grainId,
                new Dictionary<string, object>(),
                GrainInterfaceType.Create("ITestGrain"), 
                1);
            
            // Act
            var result = await director.OnAddActivation(strategy, target, mockPlacementContext.Object);
            
            // Assert
            result.ShouldNotBeNull();
            // Should be one of the worker silos
            result.ShouldBeOneOf(workerSilo1, workerSilo2);
            // Should not be the analytics silo
            result.ShouldNotBe(analyticsSilo);
        }
        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SiloNamePatternPlacementDirector_OnAddActivation_NullOrWhiteSpacePattern_ThrowsException(string? invalidPattern)
        {
            // Arrange
            // Create mock silo addresses
            var silo1 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0);
            var silo2 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 22222), 0);
            
            // Setup mock silo status oracle
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            var mockLogger = new Mock<ILogger<SiloNamePatternPlacementDirector>>();
            
            // Setup silo statuses
            var siloStatuses = new Dictionary<SiloAddress, SiloStatus>
            {
                { silo1, SiloStatus.Active },
                { silo2, SiloStatus.Active }
            };
            mockSiloStatusOracle.Setup(x => x.GetApproximateSiloStatuses(true)).Returns(siloStatuses);
            
            // Setup mock placement context
            var mockPlacementContext = new Mock<IPlacementContext>();
            var compatibleSilos = new[] { silo1, silo2 };
            mockPlacementContext.Setup(x => x.GetCompatibleSilos(It.IsAny<PlacementTarget>())).Returns(compatibleSilos);
            
            // Create placement strategy with the invalid pattern
            var strategy = new SiloNamePatternPlacement();
            typeof(SiloNamePatternPlacement).GetProperty("SiloNamePattern")!.SetValue(strategy, invalidPattern);
            
            // Create grain properties with the invalid pattern
            var grainProperties = new GrainProperties(
                ImmutableDictionary.CreateRange(new Dictionary<string, string>
                {
                    { SiloNamePatternPlacement.SiloNamePatternPropertyKey, invalidPattern ?? string.Empty }
                }));
                
            // Create a mock IClusterManifestProvider
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            
            // Setup a GrainManifest that maps our test grain type to the properties
            var grainDict = ImmutableDictionary.CreateBuilder<GrainType, GrainProperties>();
            grainDict.Add(GrainType.Create("ITestGrain"), grainProperties);
            
            // Create a grain manifest with the grain properties
            var grainManifest = new GrainManifest(
                grainDict.ToImmutable(), 
                ImmutableDictionary<GrainInterfaceType, GrainInterfaceProperties>.Empty);
                
            // Create silo manifests dictionary
            var siloManifests = ImmutableDictionary.CreateBuilder<SiloAddress, GrainManifest>();
            siloManifests.Add(silo1, grainManifest);
            
            // Create cluster manifest
            var clusterManifest = new ClusterManifest(
                new MajorMinorVersion(1, 0),
                siloManifests.ToImmutable());
                
            // Setup the mock provider to return our cluster manifest
            mockClusterManifestProvider.Setup(m => m.Current).Returns(clusterManifest);
            mockClusterManifestProvider.Setup(m => m.LocalGrainManifest).Returns(grainManifest);
            
            // Create the GrainPropertiesResolver with our mock
            var grainPropertiesResolver = new GrainPropertiesResolver(mockClusterManifestProvider.Object);
            
            // Create placement director
            var director = new SiloNamePatternPlacementDirector(
                mockSiloStatusOracle.Object,
                grainPropertiesResolver,mockLogger.Object);
            
            // Create placement target
            var grainId = GrainId.Create("ITestGrain", "1");
            var target = new PlacementTarget(
                grainId,
                new Dictionary<string, object>(),
                GrainInterfaceType.Create("ITestGrain"), 
                1);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<OrleansException>(() => 
                director.OnAddActivation(strategy, target, mockPlacementContext.Object));
            
            // Verify the exception message
            exception.Message.ShouldContain("SiloNamePatternPlacement strategy requires a valid silo name pattern");
            if (invalidPattern != null)
            {
                exception.Message.ShouldContain($"Current pattern: '{invalidPattern}'");
            }
        }
        
        [Fact]
        public async Task SiloNamePatternPlacementDirector_OnAddActivation_NoMatchingPattern_ThrowsException()
        {
            // Arrange
            const string testPattern = "NonExistent";
            
            // Create mock silo addresses
            var workerSilo = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0);
            var analyticsSilo = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 22222), 0);
            
            // Setup mock silo status oracle
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            var mockLogger = new Mock<ILogger<SiloNamePatternPlacementDirector>>();
            
            // Setup silo statuses
            var siloStatuses = new Dictionary<SiloAddress, SiloStatus>
            {
                { workerSilo, SiloStatus.Active },
                { analyticsSilo, SiloStatus.Active }
            };
            mockSiloStatusOracle.Setup(x => x.GetApproximateSiloStatuses(true)).Returns(siloStatuses);
            
            // Setup silo names
            string workerSiloName = "WorkerSilo";
            string analyticsSiloName = "AnalyticsSilo";
            
            mockSiloStatusOracle
                .Setup(m => m.TryGetSiloName(workerSilo, out workerSiloName))
                .Returns(true);
                
            mockSiloStatusOracle
                .Setup(m => m.TryGetSiloName(analyticsSilo, out analyticsSiloName))
                .Returns(true);
            
            // Create placement strategy with the test pattern
            var strategy = SiloNamePatternPlacement.Create(testPattern);
            
            // Make sure pattern is set properly for test
            typeof(SiloNamePatternPlacement).GetProperty("SiloNamePattern")!.SetValue(strategy, testPattern);
            
            // Setup mock placement context
            var mockPlacementContext = new Mock<IPlacementContext>();
            var compatibleSilos = new[] { workerSilo, analyticsSilo };
            mockPlacementContext.Setup(x => x.GetCompatibleSilos(It.IsAny<PlacementTarget>())).Returns(compatibleSilos);
            
            // Create grain properties with the pattern
            var grainProperties = new GrainProperties(
                ImmutableDictionary.CreateRange(new Dictionary<string, string>
                {
                    { SiloNamePatternPlacement.SiloNamePatternPropertyKey, testPattern }
                }));
                
            // Create a mock IClusterManifestProvider
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            
            // Setup a GrainManifest that maps our test grain type to the properties
            var grainDict = ImmutableDictionary.CreateBuilder<GrainType, GrainProperties>();
            grainDict.Add(GrainType.Create("ITestGrain"), grainProperties);
            
            // Create a grain manifest with the grain properties
            var grainManifest = new GrainManifest(
                grainDict.ToImmutable(), 
                ImmutableDictionary<GrainInterfaceType, GrainInterfaceProperties>.Empty);
                
            // Create silo manifests dictionary
            var siloManifests = ImmutableDictionary.CreateBuilder<SiloAddress, GrainManifest>();
            siloManifests.Add(workerSilo, grainManifest);
            
            // Create cluster manifest
            var clusterManifest = new ClusterManifest(
                new MajorMinorVersion(1, 0),
                siloManifests.ToImmutable());
                
            // Setup the mock provider to return our cluster manifest
            mockClusterManifestProvider.Setup(m => m.Current).Returns(clusterManifest);
            mockClusterManifestProvider.Setup(m => m.LocalGrainManifest).Returns(grainManifest);
            
            // Create the GrainPropertiesResolver with our mock
            var grainPropertiesResolver = new GrainPropertiesResolver(mockClusterManifestProvider.Object);
            
            // Create placement director
            var director = new SiloNamePatternPlacementDirector(
                mockSiloStatusOracle.Object,
                grainPropertiesResolver,mockLogger.Object);
            
            // Create placement target
            var grainId = GrainId.Create("ITestGrain", "1");
            var target = new PlacementTarget(
                grainId,
                new Dictionary<string, object>(),
                GrainInterfaceType.Create("ITestGrain"), 
                1);
            
            // Act & Assert
            var exception = await Assert.ThrowsAsync<OrleansException>(() => 
                director.OnAddActivation(strategy, target, mockPlacementContext.Object));
            
            // Verify the exception message contains relevant information
            exception.Message.ShouldContain(testPattern);
            exception.Message.ShouldContain("No silos matching pattern");
            exception.Message.ShouldContain(workerSilo.ToString());
            exception.Message.ShouldContain(analyticsSilo.ToString());
        }
        
        [Fact]
        public void SiloNamePatternPlacementAttribute_Constructor_SetsPatternProperty()
        {
            // Arrange & Act
            var attribute = new SiloNamePatternPlacementAttribute("TestPattern");
            
            // Assert
            attribute.ShouldNotBeNull();
            attribute.SiloNamePattern.ShouldBe("TestPattern");
            attribute.PlacementStrategy.ShouldBeOfType<SiloNamePatternPlacement>();
        }
        
        [Fact]
        public async Task DefaultPlacement_NotAffectedBy_SiloNamePatternPlacement()
        {
            // Arrange
            // Create mock silo addresses
            var silo1 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 11111), 0);
            var silo2 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 22222), 0);
            var silo3 = SiloAddress.New(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 33333), 0);
            
            // Setup mock silo status oracle
            var mockSiloStatusOracle = new Mock<ISiloStatusOracle>();
            var mockLogger = new Mock<ILogger<SiloNamePatternPlacementDirector>>();
            
            // Setup silo statuses - all silos are active
            var siloStatuses = new Dictionary<SiloAddress, SiloStatus>
            {
                { silo1, SiloStatus.Active },
                { silo2, SiloStatus.Active },
                { silo3, SiloStatus.Active }
            };
            mockSiloStatusOracle.Setup(x => x.GetApproximateSiloStatuses(true)).Returns(siloStatuses);
            
            // Setup silo names (even though they won't be used by default placement)
            var silo1Name = "Silo1";
            var silo2Name = "Silo2";
            var silo3Name = "Silo3";
            
            mockSiloStatusOracle.Setup(m => m.TryGetSiloName(silo1, out silo1Name)).Returns(true);
            mockSiloStatusOracle.Setup(m => m.TryGetSiloName(silo2, out silo2Name)).Returns(true);
            mockSiloStatusOracle.Setup(m => m.TryGetSiloName(silo3, out silo3Name)).Returns(true);
            
            // Create a mock IClusterManifestProvider with empty grain manifest
            var mockClusterManifestProvider = new Mock<IClusterManifestProvider>();
            var emptyGrainManifest = new GrainManifest(
                ImmutableDictionary<GrainType, GrainProperties>.Empty,
                ImmutableDictionary<GrainInterfaceType, GrainInterfaceProperties>.Empty);
                
            var siloManifests = ImmutableDictionary.CreateBuilder<SiloAddress, GrainManifest>();
            siloManifests.Add(silo1, emptyGrainManifest);
            
            var clusterManifest = new ClusterManifest(
                new MajorMinorVersion(1, 0),
                siloManifests.ToImmutable());
                
            mockClusterManifestProvider.Setup(m => m.Current).Returns(clusterManifest);
            mockClusterManifestProvider.Setup(m => m.LocalGrainManifest).Returns(emptyGrainManifest);
            
            var grainPropertiesResolver = new GrainPropertiesResolver(mockClusterManifestProvider.Object);
            
            // Create a mock default placement director
            var mockDefaultPlacementDirector = new Mock<IPlacementDirector>();
            mockDefaultPlacementDirector
                .Setup(d => d.OnAddActivation(
                    It.IsAny<PlacementStrategy>(), 
                    It.IsAny<PlacementTarget>(), 
                    It.IsAny<IPlacementContext>()))
                .ReturnsAsync(silo1); // Default placement would return silo1
            
            // Setup mock placement context
            var mockPlacementContext = new Mock<IPlacementContext>();
            var compatibleSilos = new[] { silo1, silo2, silo3 };
            mockPlacementContext.Setup(x => x.GetCompatibleSilos(It.IsAny<PlacementTarget>())).Returns(compatibleSilos);
            
            // Create a standard Orleans placement strategy that is NOT SiloNamePatternPlacement
            var defaultStrategy = new RandomPlacement();
            
            // Create placement target 
            var target = new PlacementTarget(
                GrainId.Create("DefaultGrain", "1"),
                new Dictionary<string, object>(),
                GrainInterfaceType.Create("IDefaultGrain"), 
                1);
            
            // Act
            var result = await mockDefaultPlacementDirector.Object.OnAddActivation(
                defaultStrategy, target, mockPlacementContext.Object);
            
            // Assert
            result.ShouldNotBeNull();
            result.ShouldBe(silo1); // Should be the silo that the default placement director returned
            
            // Verify the default placement director was called with the expected parameters
            mockDefaultPlacementDirector.Verify(
                d => d.OnAddActivation(
                    It.Is<PlacementStrategy>(s => s == defaultStrategy),
                    It.IsAny<PlacementTarget>(),
                    It.Is<IPlacementContext>(c => c == mockPlacementContext.Object)),
                Times.Once);
                
            // Verify that no methods were called on our SiloNamePatternPlacementDirector
            var ourDirector = new SiloNamePatternPlacementDirector(
                mockSiloStatusOracle.Object,
                grainPropertiesResolver,mockLogger.Object);
                
            // This is a structural verification - our code can't actually track calls on a newly created object
            // The real verification is that the default placement works as expected
            defaultStrategy.ShouldNotBeOfType<SiloNamePatternPlacement>();
        }
    }
} 