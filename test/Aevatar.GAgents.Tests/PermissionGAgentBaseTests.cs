using Aevatar.Core.Abstractions;
using Aevatar.PermissionManagement;
using Shouldly;

namespace Aevatar.GAgents.Tests;

[GenerateSerializer]
public class TestPermissionState : PermissionStateBase
{
}

[GenerateSerializer]
public class TestStateLogEvent : StateLogEventBase<TestStateLogEvent>
{
}

public interface ITestPermissionGAgent : IGAgent
{
    Task<TestPermissionState> GetStateAsync();
    Task AddTestUsersAsync(params Guid[] userIds);
    Task RemoveTestUsersAsync(params Guid[] userIds);
}

[GAgent]
public class TestPermissionGAgent : PermissionGAgentBase<TestPermissionState, TestStateLogEvent>, ITestPermissionGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Test Permission GAgent");
    }

    public Task<TestPermissionState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task AddTestUsersAsync(params Guid[] userIds)
    {
        return AddAuthorizedUsersAsync(userIds);
    }

    public Task RemoveTestUsersAsync(params Guid[] userIds)
    {
        return RemoveAuthorizedUsersAsync(userIds);
    }
}

public class PermissionGAgentBaseTests : AevatarGAgentsTestBase
{
    private readonly IGAgentFactory _gAgentFactory;

    public PermissionGAgentBaseTests()
    {
        _gAgentFactory = GetRequiredService<IGAgentFactory>();
    }

    [Fact]
    public async Task AddAuthorizedUsers_Should_Work()
    {
        // Arrange
        var gAgent = await _gAgentFactory.GetGAgentAsync<ITestPermissionGAgent>();
        var testUserId1 = Guid.NewGuid();
        var testUserId2 = Guid.NewGuid();

        // Act
        await gAgent.AddTestUsersAsync(testUserId1, testUserId2);
        var state = await gAgent.GetStateAsync();

        // Assert
        state.IsPublic.ShouldBeFalse();
        state.AuthorizedUserIds.ShouldContain(testUserId1);
        state.AuthorizedUserIds.ShouldContain(testUserId2);
        state.AuthorizedUserIds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveAuthorizedUsers_Should_Work()
    {
        // Arrange
        var gAgent = await _gAgentFactory.GetGAgentAsync<ITestPermissionGAgent>();
        var testUserId1 = Guid.NewGuid();
        var testUserId2 = Guid.NewGuid();
        await gAgent.AddTestUsersAsync(testUserId1, testUserId2);

        // Act
        await gAgent.RemoveTestUsersAsync(testUserId1);
        var state = await gAgent.GetStateAsync();

        // Assert
        state.IsPublic.ShouldBeFalse();
        state.AuthorizedUserIds.ShouldNotContain(testUserId1);
        state.AuthorizedUserIds.ShouldContain(testUserId2);
        state.AuthorizedUserIds.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveAllAuthorizedUsers_Should_MakePublic()
    {
        // Arrange
        var gAgent = await _gAgentFactory.GetGAgentAsync<ITestPermissionGAgent>();
        var testUserId = Guid.NewGuid();
        await gAgent.AddTestUsersAsync(testUserId);

        // Act
        await gAgent.RemoveTestUsersAsync(testUserId);
        var state = await gAgent.GetStateAsync();

        // Assert
        state.IsPublic.ShouldBeTrue();
        state.AuthorizedUserIds.Count.ShouldBe(0);
    }
} 