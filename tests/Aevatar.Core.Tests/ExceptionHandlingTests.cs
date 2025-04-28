using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Orleans.Runtime;
using Orleans.Streams;
using System.Threading.Tasks;
using Xunit;

namespace Aevatar.Core.Tests;

public class ExceptionHandlingTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;
    private readonly Mock<ILogger<EventWrapperBaseAsyncObserver>> _loggerMock;
    private readonly Mock<IStreamProvider> _streamProviderMock;
    private readonly Mock<IAsyncStream<EventWrapperBase>> _streamMock;

    public ExceptionHandlingTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerFactoryMock = new Mock<ILoggerFactory>();
        _loggerMock = new Mock<ILogger<EventWrapperBaseAsyncObserver>>();
        _streamProviderMock = new Mock<IStreamProvider>();
        _streamMock = new Mock<IAsyncStream<EventWrapperBase>>();

        _loggerFactoryMock.Setup(f => f.CreateLogger<EventWrapperBaseAsyncObserver>())
            .Returns(_loggerMock.Object);
        _serviceProviderMock.Setup(sp => sp.GetService(typeof(ILoggerFactory)))
            .Returns(_loggerFactoryMock.Object);
        _streamProviderMock.Setup(sp => sp.GetStream<EventWrapperBase>(It.IsAny<StreamId>()))
            .Returns(_streamMock.Object);
    }

    [Fact]
    public async Task GAgentAsyncObserver_ShouldPublishExceptionToStream_WhenExceptionOccurs()
    {
        // Arrange
        var grainId = "TestGrain";
        var exception = new InvalidOperationException("Test exception");
        var eventWrapper = new EventWrapper<TestEvent>(new TestEvent(), Guid.NewGuid(), GrainId.Create("TestType", grainId));
        
        var observers = new List<EventWrapperBaseAsyncObserver>
        {
            EventWrapperBaseAsyncObserver.Create(
                _ => throw exception,
                _serviceProviderMock.Object,
                "TestMethod",
                "TestEvent"
            )
        };

        var observer = new GAgentAsyncObserver(observers, grainId, _streamProviderMock.Object);

        // Set up the stream to verify it receives the exception event
        _streamMock.Setup(s => s.OnNextAsync(It.IsAny<EventWrapperBase>(), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await observer.OnNextAsync(eventWrapper);
        });

        // Verify the exception was published to the stream
        _streamMock.Verify(s => s.OnNextAsync(
            It.Is<EventWrapperBase>(e => EventContainsException(e, exception)),
            null),
            Times.Once);
    }

    [Fact]
    public async Task GAgentBase_InvokeWithExceptionPublishingAsync_ShouldPublishExceptionAndRethrow()
    {
        // Arrange
        var testGAgent = new TestGAgent(_streamProviderMock.Object, _serviceProviderMock.Object);
        var exception = new InvalidOperationException("Test exception");
        
        // Set up the stream to verify it receives the exception event
        _streamMock.Setup(s => s.OnNextAsync(It.IsAny<EventWrapperBase>(), null))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await testGAgent.TestInvokeWithExceptionPublishingAsync(() => throw exception);
        });

        // Verify the exception was published to the stream
        _streamMock.Verify(s => s.OnNextAsync(
            It.Is<EventWrapperBase>(e => EventContainsException(e, exception)),
            null),
            Times.Once);
    }

    private bool EventContainsException(EventWrapperBase wrapper, Exception exception)
    {
        // Extract the event from the wrapper using reflection
        var eventProperty = wrapper.GetType().GetProperty("Event");
        if (eventProperty == null) return false;
        
        var eventValue = eventProperty.GetValue(wrapper);
        if (eventValue is not GAgentExceptionEvent exceptionEvent) return false;
        
        // Verify the exception event contains the expected exception details
        return exceptionEvent.ExceptionType == exception.GetType().FullName &&
               exceptionEvent.ExceptionMessage == exception.Message;
    }

    [GenerateSerializer]
    private class TestEvent : EventBase { }

    // Test GAgent implementation for testing
    private class TestGAgent : GAgentBase<TestState, TestStateLogEvent, TestEvent, ConfigurationBase>
    {
        public TestGAgent(IStreamProvider streamProvider, IServiceProvider serviceProvider)
        {
            SetStreamProvider(streamProvider);
            SetServiceProvider(serviceProvider);
        }

        public void SetStreamProvider(IStreamProvider streamProvider)
        {
            var field = typeof(GAgentBase<TestState, TestStateLogEvent, TestEvent, ConfigurationBase>)
                .GetField("LazyStreamProvider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, new Lazy<IStreamProvider>(() => streamProvider));
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            var property = typeof(GAgentBase<TestState, TestStateLogEvent, TestEvent, ConfigurationBase>)
                .GetProperty("ServiceProvider");
            property?.SetValue(this, serviceProvider);
        }

        public async Task TestInvokeWithExceptionPublishingAsync(Func<Task> action)
        {
            await InvokeWithExceptionPublishingAsync(action, "TestMethod");
        }

        public async Task<T> TestInvokeWithExceptionPublishingAsync<T>(Func<Task<T>> action)
        {
            return await InvokeWithExceptionPublishingAsync(action, "TestMethod");
        }
        
        protected override Task OnActivateAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [GenerateSerializer]
    private class TestState : StateBase { }

    [GenerateSerializer]
    private class TestStateLogEvent : StateLogEventBase<TestStateLogEvent> { }
} 