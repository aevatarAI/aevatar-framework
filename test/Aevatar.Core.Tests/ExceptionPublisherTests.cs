using System.Text.Json;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Tests.TestGAgents;
using Microsoft.Extensions.Options;
using Orleans.TestKit;
using Orleans.TestKit.Streams;
using Shouldly;

namespace Aevatar.Core.Tests;

public class ExceptionPublisherTests : GAgentTestKitBase
{
    private ExceptionHandlingTestGAgent _agent;
    private TestStream<ExceptionEvent> _exceptionStream;

    public ExceptionPublisherTests ()
    {
        var options = new AevatarOptions
        {
            ExceptionStreamNamespace = "AevatarException",
            ExceptionStreamKey = "global-exceptions"
        };
        Silo.ServiceProvider.AddService<IOptions<AevatarOptions>>(Options.Create(options));

        // Create the test exception stream
        _exceptionStream = Silo.AddStreamProbe<ExceptionEvent>(
            StreamId.Create(options.ExceptionStreamNamespace, options.ExceptionStreamKey));

    }

    [Fact]
    public async Task PublishExceptionAsync_ShouldPublishExceptionToStream()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());
        
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var contextData = new { TestId = 1, Message = "Test context" };

        // Act
        var exceptionId = await _agent.TestPublishExceptionAsync(exception, contextData);

        // Assert
        exceptionId.ShouldNotBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(1);
        
        _exceptionStream.VerifySend(e => 
            e.ExceptionMessage == exception.Message && 
            e.ExceptionType == exception.GetType().FullName &&
            e.CorrelationId == exceptionId &&
            e.ContextData.Contains("TestId") &&
            e.ContextData.Contains("Test context")
        );
    }

    [Fact]
    public async Task CatchAndPublishExceptionAsync_WithoutException_ShouldReturnEmptyGuid()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act
        var exceptionId = await _agent.TestCatchAndPublishWithoutResultAsync(false);

        // Assert
        exceptionId.ShouldBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(0);
    }

    [Fact]
    public async Task CatchAndPublishExceptionAsync_WithException_ShouldPublishAndNotRethrow()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act
        var exceptionId = await _agent.TestCatchAndPublishWithoutResultAsync(true, false);

        // Assert
        exceptionId.ShouldNotBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(1);
        _exceptionStream.VerifySend(e => 
            e.ExceptionMessage == "Test exception" && 
            e.ExceptionType == typeof(InvalidOperationException).FullName &&
            e.CorrelationId == exceptionId
        );
    }

    [Fact]
    public async Task CatchAndPublishExceptionAsync_WithException_ShouldPublishAndRethrow()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _agent.TestCatchAndPublishWithoutResultAsync(true, true));
        
        exception.Message.ShouldBe("Test exception");
        _exceptionStream.Sends.ShouldBe(1);
        _exceptionStream.VerifySend(e => e.ExceptionMessage == "Test exception");
    }

    [Fact]
    public async Task CatchAndPublishExceptionWithResult_WithoutException_ShouldReturnResultAndEmptyGuid()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act
        var (result, exceptionId) = await _agent.TestCatchAndPublishWithResultAsync(false);

        // Assert
        result.ShouldBe(42);
        exceptionId.ShouldBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(0);
    }

    [Fact]
    public async Task CatchAndPublishExceptionWithResult_WithException_ShouldReturnDefaultAndPublish()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act
        var (result, exceptionId) = await _agent.TestCatchAndPublishWithResultAsync(true, false);

        // Assert
        result.ShouldBe(0); // Default value for int
        exceptionId.ShouldNotBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(1);
        _exceptionStream.VerifySend(e => 
            e.ExceptionMessage == "Test exception" && 
            e.ExceptionType == typeof(InvalidOperationException).FullName &&
            e.CorrelationId == exceptionId
        );
    }

    [Fact]
    public async Task CatchAndPublishExceptionWithResult_WithExceptionAndRethrow_ShouldPublishAndRethrow()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _agent.TestCatchAndPublishWithResultAsync(true, true));
        
        exception.Message.ShouldBe("Test exception");
        _exceptionStream.Sends.ShouldBe(1);
        _exceptionStream.VerifySend(e => e.ExceptionMessage == "Test exception");
    }

    [Fact]
    public async Task CatchAndPublishExceptionWithResult_WithCustomDefault_ShouldReturnCustomDefault()
    {
        // Create test agent
        _agent = await Silo.CreateGrainAsync<ExceptionHandlingTestGAgent>(Guid.NewGuid());

        // Arrange & Act
        var (result, exceptionId) = await _agent.TestCatchAndPublishWithCustomDefaultAsync();

        // Assert
        result.ShouldBe(999);
        exceptionId.ShouldNotBe(Guid.Empty);
        _exceptionStream.Sends.ShouldBe(1);
    }
} 