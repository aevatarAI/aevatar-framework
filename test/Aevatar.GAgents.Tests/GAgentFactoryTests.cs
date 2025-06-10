using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestArtifacts;
using Aevatar.Core.Tests.TestGAgents;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Metadata;
using Shouldly;
using Xunit.Abstractions;

namespace Aevatar.GAgents.Tests;

public sealed class GAgentFactoryTests : AevatarGAgentsTestBase
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IGAgentFactory _gAgentFactory;
    private readonly IGAgentManager _gAgentManager;
    private readonly GrainTypeResolver _grainTypeResolver;

    public GAgentFactoryTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _gAgentManager = GetRequiredService<IGAgentManager>();
        var clusterClient = GetRequiredService<IClusterClient>();
        _grainTypeResolver = clusterClient.ServiceProvider.GetRequiredService<GrainTypeResolver>();
    }

    [Fact(DisplayName = "Can create GAgent by GrainId.")]
    public async Task CreateGAgentByGrainIdTest()
    {
        var guid = Guid.NewGuid().ToString("N");
        var grainId = GrainId.Create("test.group", guid);
        var gAgent = await _gAgentFactory.GetGAgentAsync(grainId);
        gAgent.GetPrimaryKey().ToString("N").ShouldBe(guid);
        gAgent.ShouldNotBeNull();
        gAgent.GetGrainId().ShouldBe(grainId);
        await CheckSubscribedEventsAsync(gAgent);
    }

    [Fact(DisplayName = "Can create GAgent by generic type.")]
    public async Task CreateGAgentByGenericTypeTest()
    {
        {
            var gAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<GroupGAgentState>>(Guid.NewGuid());
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            gAgent.GetGrainId().ShouldBe(GrainId.Create("test.group", gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<NaiveTestGAgentState>>();
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            gAgent.GetGrainId().ShouldBe(GrainId.Create("Aevatar.Core.Tests.TestGAgents.naiveTest",
                gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            gAgent.GetGrainId().ShouldBe(GrainId.Create("Aevatar.Core.PublishingGAgent",
                gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }
    }

    [Fact(DisplayName = "Can create GAgent and execute PerformConfigAsync method.")]
    public async Task CreateGAgentWithConfigurationTest()
    {
        // Arrange & Act.
        var guid = Guid.NewGuid();
        var gAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<NaiveTestGAgentState>>(guid,
            new NaiveGAgentConfiguration
            {
                Greeting = "Test"
            });

        var configurationType = await gAgent.GetConfigurationTypeAsync();
        configurationType.ShouldBe(typeof(NaiveGAgentConfiguration));

        await TestHelper.WaitUntilAsync(_ => CheckState(gAgent), TimeSpan.FromSeconds(20));

        // Assert.
        Should.NotThrow(() => gAgent.GetPrimaryKey());
        await CheckSubscribedEventsAsync(gAgent);
        gAgent.GetGrainId().ShouldBe(GrainId.Create("Aevatar.Core.Tests.TestGAgents.naiveTest",
            gAgent.GetPrimaryKey().ToString("N")));
        var gAgentState = await gAgent.GetStateAsync();
        gAgentState.Content.Count.ShouldBe(1);
        gAgentState.Content.First().ShouldBe("Test");
    }

    [Fact(DisplayName = "Can create GAgent by alias.")]
    public async Task CreateGAgentByAliasTest()
    {
        {
            var gAgent =
                await _gAgentFactory.GetGAgentAsync(Guid.NewGuid(), "naiveTest", "Aevatar.Core.Tests.TestGAgents");
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync("naiveTest", "Aevatar.Core.Tests.TestGAgents",
                configuration: new NaiveGAgentConfiguration
                {
                    Greeting = "Test"
                });
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync("groupTest", "test");
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            await CheckSubscribedEventsAsync(gAgent);
        }
    }

    [Fact(DisplayName = "Can create GAgent by GAgent type.")]
    public async Task CreateGAgentByGAgentType()
    {
        var guid = Guid.NewGuid();
        var gAgent1 = await _gAgentFactory.GetGAgentAsync(guid, nameof(FatalEventHandlerTestGAgent),
            typeof(FatalEventHandlerTestGAgent).Namespace!);
        var gAgent2 = await _gAgentFactory.GetGAgentAsync(guid, typeof(FatalEventHandlerTestGAgent));
        gAgent1.GetPrimaryKey().ShouldBe(guid);
        gAgent2.GetPrimaryKey().ShouldBe(guid);
        await CheckSubscribedEventsAsync(gAgent1);
        await CheckSubscribedEventsAsync(gAgent2);
    }

    [Fact(DisplayName = "The implementation of GetInitializeDtoTypeAsync works.")]
    public async Task GetInitializeDtoTypeTest()
    {
        var gAgent =
            await _gAgentFactory.GetGAgentAsync(Guid.NewGuid(), "configurationTest", "Aevatar.Core.Tests.TestGAgents");
        var initializeDtoType = await gAgent.GetConfigurationTypeAsync();
        initializeDtoType.ShouldBe(typeof(Configuration));
    }

    [Fact(DisplayName = "The implementation of GetAvailableGAgentTypes works.")]
    public async Task GetAvailableGAgentTypesTest()
    {
        var availableGAgentTypes = _gAgentManager.GetAvailableGAgentTypes();
        availableGAgentTypes.Count.ShouldBeGreaterThan(20);
        foreach (var gAgentType in availableGAgentTypes)
        {
            var gAgentGrainType = _grainTypeResolver.GetGrainType(gAgentType);
            _outputHelper.WriteLine($"{gAgentType}: {gAgentGrainType}");
        }
    }

    [Fact(DisplayName = "The implementation of GetAvailableGAgentGrainTypes works.")]
    public async Task GetAvailableGAgentGrainTypesTest()
    {
        var availableGAgentGrainTypes = _gAgentManager.GetAvailableGAgentGrainTypes();
        availableGAgentGrainTypes.Count.ShouldBeGreaterThan(20);
        foreach (var grainType in availableGAgentGrainTypes.Select(gAgentGrainType => gAgentGrainType.ToString()))
        {
            _outputHelper.WriteLine(grainType);
            if (grainType!.StartsWith("test"))
            {
                var grainId = GrainId.Create(grainType, Guid.NewGuid().ToString());
                var gAgent = await _gAgentFactory.GetGAgentAsync(grainId);
                await CheckSubscribedEventsAsync(gAgent);
            }
        }
    }
    
    [Fact(DisplayName = "The implementation of GetAvailableEventTypes works.")]
    public async Task GetAvailableEventTypesTest()
    {
        var availableEventTypes = _gAgentManager.GetAvailableEventTypes();
        availableEventTypes.Count.ShouldBeGreaterThan(20);
        foreach (var eventType in availableEventTypes.Select(eventType => eventType.Name))
        {
            _outputHelper.WriteLine(eventType);
        }
    }

    [Fact(DisplayName = "Can create ArtifactGAgent.")]
    public async Task GetArtifactGAgentTest()
    {
        {
            var artifactGAgent = await _gAgentFactory
                .GetGAgentAsync<IArtifactGAgent<MyArtifact, MyArtifactGAgentState, MyArtifactStateLogEvent>>();
            var events = await artifactGAgent.GetAllSubscribedEventsAsync();
            events.ShouldNotBeEmpty();

            var desc = await artifactGAgent.GetDescriptionAsync();
            desc.ShouldBe("MyArtifact Description, this is for testing.");

            var artifact = await artifactGAgent.GetArtifactAsync();
            artifact.ShouldNotBeNull();
        }

        {
            var artifactGAgent = await _gAgentFactory
                .GetArtifactGAgentAsync<MyArtifact, MyArtifactGAgentState, MyArtifactStateLogEvent>();
            var events = await artifactGAgent.GetAllSubscribedEventsAsync();
            events.ShouldNotBeEmpty();
        }
    }

    private async Task<bool> CheckState(IStateGAgent<NaiveTestGAgentState> gAgent)
    {
        var state = await gAgent.GetStateAsync();
        return !state.Content.IsNullOrEmpty();
    }

    private async Task CheckSubscribedEventsAsync(IGAgent gAgent)
    {
        var subscribedEvents = await gAgent.GetAllSubscribedEventsAsync(true);
        subscribedEvents.ShouldNotBeNull();
        subscribedEvents.Count.ShouldBePositive();
    }
}