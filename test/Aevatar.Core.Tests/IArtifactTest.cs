using Aevatar.Core.Artifact;
using Aevatar.Agent.Abstractions;
using Moq;
using Xunit;
using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Artifact.Tests;

public class MockState : IGAgentState
{
    public List<GrainId> Children { get; set; } = new List<GrainId>();
    public GrainId? Parent { get; set; }
    public string? GAgentCreator { get; set; } = default!;
    public Guid Id { get; set; }
    public DateTime Ctime { get; set; }
}

[GenerateSerializer]
public abstract class MockStateLogEventBase : IGAgentEventBase
{
    [Id(0)] public virtual Guid Id { get; set; }
    [Id(1)] public DateTime Ctime { get; set; }
}

[GenerateSerializer]
public abstract class MockStateLogEventBase<T> : MockStateLogEventBase,IGAgentEventBase<T>
    where T : MockStateLogEventBase<T>;

[GenerateSerializer]
public class MockArtifactState : MockState;

[GenerateSerializer]
public class MockArtifactStateLogEventBase : MockStateLogEventBase<MockArtifactStateLogEventBase>;

[GenerateSerializer]
public class MockArtifact: IArtifact<MockArtifactState, MockArtifactStateLogEventBase>
{
    public virtual void TransitionState(IGAgentState state, IGAgentEventBase<MockArtifactStateLogEventBase> stateLogEvent)
    {
        
    }

    public virtual string GetDescription()
    {
        return "Test Description";
    }
}

public class IArtifactTests
{
    [Fact]
    public void TransitionState_ShouldInvokeWithCorrectParameters()
    {
        // Arrange
        var mockState = new Mock<MockArtifactState>();
        var mockEvent = new Mock<MockArtifactStateLogEventBase>();
        var mockArtifact = new Mock<MockArtifact>();

        // Act
        mockArtifact.Object.TransitionState(mockState.Object, mockEvent.Object);

        // Assert
        mockArtifact.Verify(a => a.TransitionState(mockState.Object, mockEvent.Object), Times.Once);
    }

    [Fact]
    public void GetDescription_ShouldReturnExpectedValue()
    {
        // Arrange
        var expectedDescription = "Test Description";
        var mockState = new Mock<MockArtifactState>();
        var mockArtifact = new Mock<MockArtifact>();
        mockArtifact.Setup(a => a.GetDescription()).Returns(expectedDescription);

        // Act
        var description = mockArtifact.Object.GetDescription();

        // Assert
        Assert.Equal(expectedDescription, description);
    }

    [Fact]
    public void ArtifactClassType_ShouldNotInheritBaseClassesInCoreAbstractNamespace()
    {
        // Arrange
        var mockState = new Mock<MockArtifactState>();
        var mockEvent = new Mock<MockArtifactStateLogEventBase>();
        var mockArtifact = new Mock<MockArtifact>();


        Console.WriteLine(mockArtifact.Object.GetType().Name);
        //Assert
        Assert.True(mockArtifact.Object is IArtifact<MockArtifactState, MockArtifactStateLogEventBase>);
        Assert.False(mockState.Object is StateBase);
        Assert.False(mockEvent.Object is StateLogEventBase);
    }
}