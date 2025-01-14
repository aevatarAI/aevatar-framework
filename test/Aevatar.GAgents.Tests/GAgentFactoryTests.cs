using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.Core.Tests.TestInitializeDtos;
using Aevatar.Core.Tests.TestStates;
using Shouldly;

namespace Aevatar.GAgents.Tests;

public sealed class GAgentFactoryTests : AevatarGAgentsTestBase
{
    private readonly IGAgentFactory _gAgentFactory;
    private readonly IGAgentManager _gAgentManager;

    public GAgentFactoryTests()
    {
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
        _gAgentManager = GetRequiredService<IGAgentManager>();
    }

    [Fact(DisplayName = "Can create GAgent by GrainId.")]
    public async Task CreateGAgentByGrainIdTest()
    {
        var guid = Guid.NewGuid().ToString("N");
        var grainId = GrainId.Create("test/group", guid);
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
            gAgent.GetGrainId().ShouldBe(GrainId.Create("test/group", gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<ProxyTestGAgentState>>();
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            gAgent.GetGrainId().ShouldBe(GrainId.Create("aevatar/naiveTest", gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync<IPublishingGAgent>();
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            gAgent.GetGrainId().ShouldBe(GrainId.Create("aevatar/publishing", gAgent.GetPrimaryKey().ToString("N")));
            await CheckSubscribedEventsAsync(gAgent);
        }
    }

    [Fact(DisplayName = "Can create GAgent and execute InitializeAsync method.")]
    public async Task CreateGAgentWithInitializeMethodTest()
    {
        // Arrange & Act.
        var guid = Guid.NewGuid();
        var gAgent = await _gAgentFactory.GetGAgentAsync<IStateGAgent<ProxyTestGAgentState>>(guid,
            new NaiveGAgentInitializationEvent
            {
                InitialGreeting = "Test"
            });

        var initializeDtoType = await gAgent.GetInitializationTypeAsync();
        initializeDtoType.ShouldBe(typeof(NaiveGAgentInitializationEvent));

        await TestHelper.WaitUntilAsync(_ => CheckState(gAgent), TimeSpan.FromSeconds(20));

        // Assert.
        Should.NotThrow(() => gAgent.GetPrimaryKey());
        await CheckSubscribedEventsAsync(gAgent);
        gAgent.GetGrainId().ShouldBe(GrainId.Create("aevatar/naiveTest", gAgent.GetPrimaryKey().ToString("N")));
        var gAgentState = await gAgent.GetStateAsync();
        gAgentState.Content.Count.ShouldBe(1);
        gAgentState.Content.First().ShouldBe("Test");
    }

    [Fact(DisplayName = "Can create GAgent by alias.")]
    public async Task CreateGAgentByAliasTest()
    {
        {
            var gAgent = await _gAgentFactory.GetGAgentAsync("naiveTest", Guid.NewGuid());
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            await CheckSubscribedEventsAsync(gAgent);
        }

        {
            var gAgent = await _gAgentFactory.GetGAgentAsync("naiveTest", initializeDto: new NaiveGAgentInitializationEvent
            {
                InitialGreeting = "Test"
            });
            gAgent.ShouldNotBeNull();
            Should.NotThrow(() => gAgent.GetPrimaryKey());
            await CheckSubscribedEventsAsync(gAgent);
        }
    }

    [Fact(DisplayName = "The implementation of GetInitializeDtoTypeAsync works.")]
    public async Task GetInitializeDtoTypeTest()
    {
        var gAgent = await _gAgentFactory.GetGAgentAsync("initialize", Guid.NewGuid());
        var initializeDtoType = await gAgent.GetInitializationTypeAsync();
        initializeDtoType.ShouldBe(typeof(Initialize));
    }

    [Fact(DisplayName = "The implementation of GetAvailableGAgentTypes works.")]
    public async Task GetAvailableGAgentTypesTest()
    {
        var availableGAgents = _gAgentManager.GetAvailableGAgentTypes();
        availableGAgents.Count.ShouldBeGreaterThan(20);
    }

    private async Task<bool> CheckState(IStateGAgent<ProxyTestGAgentState> gAgent)
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