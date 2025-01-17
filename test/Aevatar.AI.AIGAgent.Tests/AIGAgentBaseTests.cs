using Aevatar.AI.Agent;
using Aevatar.AI.Brain;
using Aevatar.AI.BrainFactory;
using Aevatar.AI.Dtos;
using Aevatar.AI.State;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Castle.Core.Logging;
using Moq;
using File = Aevatar.AI.Brain;

namespace Aevatar.AI.AIGAgent.Tests;

public interface ITestAIAgent : IAIGAgent
{
    Task<string?> PublicInvokePromptAsync(string prompt);
}

public class TestAIGAgentStateLogEvent : StateLogEventBase<TestAIGAgentStateLogEvent>
{
}

[GenerateSerializer]
public class TestAIGAgentState : AIGAgentStateBase
{
    [Id(0)]  public List<string> Content { get; set; }
}

public class TestAIGAgent : AIGAgentBase<TestAIGAgentState, TestAIGAgentStateLogEvent>, ITestAIAgent
{
    public TestAIGAgent(ILogger logger) : base(logger)
    {
    }

    public Task<string?> PublicInvokePromptAsync(string prompt)
    {
        return InvokePromptAsync(prompt);
    }

    public override Task<string> GetDescriptionAsync()
    {
        throw new NotImplementedException();
    }
}

public class AIGAgentBaseTests : AevatarGAgentsTestBase
{
    protected readonly IGrainFactory _grainFactory;
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IBrainFactory> _brainFactoryMock;
    private readonly Mock<IBrain> _brainMock;

    public AIGAgentBaseTests()
    {
        _grainFactory = GetRequiredService<IGrainFactory>();
    }

    [Fact]
    public async Task InitializeAsync_WithValidInput_ShouldReturnTrue()
    {
        // Arrange
        var initializeDto = new InitializeDto
        {
            LLM = "gpt-4",
            Instructions = "Test instructions",
            Files = new List<FileDto>
            {
                new() { 
                    Content = System.Text.Encoding.UTF8.GetBytes("content"), 
                    Type = "text", 
                    Name = "test.txt" 
                }
            }
        };

        _brainFactoryMock
            .Setup(x => x.GetBrain(initializeDto.LLM))
            .Returns(_brainMock.Object);

        _brainMock
            .Setup(x => x.InitializeAsync(
                It.IsAny<string>(),
                initializeDto.Instructions,
                It.IsAny<List<File.File>>()))
            .ReturnsAsync(true);
        
        var agent = _grainFactory.GetGrain<ITestAIAgent>(Guid.NewGuid());
        
        // Act
        var result = await agent.InitializeAsync(initializeDto);

        // Assert
        result.ShouldBeTrue();
        _brainFactoryMock.Verify(x => x.GetBrain(initializeDto.LLM), Times.Once);
        _brainMock.Verify(
            x => x.InitializeAsync(
                It.IsAny<string>(),
                initializeDto.Instructions,
                It.IsAny<List<File>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WhenBrainFactoryReturnsNull_ShouldReturnFalse()
    {
        // Arrange
        var initializeDto = new InitializeDto
        {
            LLM = "invalid-model",
            Instructions = "Test instructions",
            Files = new List<FileDto>()
        };

        _brainFactoryMock
            .Setup(x => x.GetBrain(initializeDto.LLM))
            .Returns((IBrain?)null);

        // Act
        var result = await _agent.InitializeAsync(initializeDto);

        // Assert
        result.ShouldBeFalse();
        _brainFactoryMock.Verify(x => x.GetBrain(initializeDto.LLM), Times.Once);
    }

    [Fact]
    public async Task InvokePromptAsync_WithInitializedBrain_ShouldReturnResponse()
    {
        // Arrange
        var expectedResponse = "Test response";
        var prompt = "Test prompt";

        // First initialize the brain
        var initializeDto = new InitializeDto
        {
            LLM = "gpt-4",
            Instructions = "Test instructions",
            Files = new List<FileDto>()
        };

        _brainFactoryMock
            .Setup(x => x.GetBrain(initializeDto.LLM))
            .Returns(_brainMock.Object);

        _brainMock
            .Setup(x => x.InitializeAsync(
                It.IsAny<string>(),
                initializeDto.Instructions,
                It.IsAny<List<File>>()))
            .ReturnsAsync(true);

        _brainMock
            .Setup(x => x.InvokePromptAsync(prompt))
            .ReturnsAsync(expectedResponse);

        await _agent.InitializeAsync(initializeDto);

        // Act
        var result = await _agent.PublicInvokePromptAsync(prompt);

        // Assert
        result.ShouldBe(expectedResponse);
        _brainMock.Verify(x => x.InvokePromptAsync(prompt), Times.Once);
    }

    [Fact]
    public async Task InvokePromptAsync_WithoutInitializedBrain_ShouldReturnNull()
    {
        // Act
        var result = await _agent.PublicInvokePromptAsync("Test prompt");

        // Assert
        result.ShouldBeNull();
    }
}