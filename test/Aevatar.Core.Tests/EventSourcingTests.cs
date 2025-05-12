using Aevatar.Core.Tests.TestEvents;
using Aevatar.Core.Tests.TestGAgents;
using Aevatar.EventSourcing.Core;
using Aevatar.GAgents.Tests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Shouldly;

namespace Aevatar.Core.Tests;

[Trait("Category", "BVT")]
[Collection("Non-Parallel Collection")]
public class EventSourcingTests : GAgentTestKitBase
{
    [Fact(DisplayName = "Implementation of LogViewAdaptor works.")]
    public async Task LogViewAdaptorTest()
    {
        // Arrange.
        var guid = Guid.NewGuid();
        var logViewGAgent = await Silo.CreateGrainAsync<LogViewAdaptorTestGAgent>(guid);
        var groupGAgent = await CreateGroupGAgentAsync(logViewGAgent);
        var publishingGAgent = await CreatePublishingGAgentAsync(groupGAgent);

        AddProbesByGrainId(publishingGAgent, groupGAgent, logViewGAgent);

        // Act: First event.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "First event"
        });

        // Act: Deactivate and re-activate the logViewGAgent.
        // TODO: Modify this, because Aevatar.TestKit cannot execute OnActivateAsync.
        //await Silo.DeactivateAsync(logViewGAgent);
        logViewGAgent = await Silo.CreateGrainAsync<LogViewAdaptorTestGAgent>(guid);

        // Act: Second event.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Second event"
        });

        // Assert.
        {
            await TestHelper.WaitUntilAsync(_ => CheckCount(logViewGAgent, 2));
            (await logViewGAgent.GetStateAsync()).Content.Count.ShouldBe(2);
        }

        // Act: Third event.
        await publishingGAgent.PublishEventAsync(new NaiveTestEvent
        {
            Greeting = "Third event"
        });

        // Assert.
        {
            await TestHelper.WaitUntilAsync(_ => CheckCount(logViewGAgent, 3));
            (await logViewGAgent.GetStateAsync()).Content.Count.ShouldBe(3);
        }

        const int minimum = 1; // SetParent or AddChildren event.
        // Asset: Check the log storage.
        InMemoryLogConsistentStorage.Storage.Count.ShouldBeGreaterThanOrEqualTo(3);
        InMemoryLogConsistentStorage.Storage.ShouldContainKey(GetStreamName(logViewGAgent.GetGrainId()));
        InMemoryLogConsistentStorage.Storage[GetStreamName(logViewGAgent.GetGrainId())].Count.ShouldBe(minimum + 3);
        InMemoryLogConsistentStorage.Storage.ShouldContainKey(GetStreamName(groupGAgent.GetGrainId()));
        InMemoryLogConsistentStorage.Storage[GetStreamName(groupGAgent.GetGrainId())].Count.ShouldBe(minimum + 2);
        InMemoryLogConsistentStorage.Storage.ShouldContainKey(GetStreamName(publishingGAgent.GetGrainId()));
        InMemoryLogConsistentStorage.Storage[GetStreamName(publishingGAgent.GetGrainId())].Count.ShouldBe(minimum);

        var resultList = await Silo.TestLogConsistentStorage.ReadAsync<LogEntry>("", logViewGAgent.GetGrainId(), 0, 10);
        resultList.Count.ShouldBeGreaterThanOrEqualTo(3);
        
        resultList = await Silo.TestLogConsistentStorage.ReadAsync<LogEntry>("", logViewGAgent.GetGrainId(), 0, -1);
        resultList.Count.ShouldBe(0);
    }
    
    [Fact]
    public async Task ExceptionLogTests()
    {
        Silo.ProtocolServices.ProtocolError("test message", false);
        var exception = Assert.Throws<OrleansException>(() => Silo.ProtocolServices.ProtocolError("test message", true));
        exception.Message.ShouldContain("test message");
        
        // can print error log
        Silo.ProtocolServices.CaughtException("test message", new Exception());
        Silo.ProtocolServices.CaughtUserCodeException("", "", new Exception());
        Silo.ProtocolServices.Log(LogLevel.Debug,"", null);
        
        Silo.ProtocolServices.GrainId.ShouldBe(new GrainId());
        Silo.ProtocolServices.MyClusterId.ShouldBe("Unknown");
    }

    [Fact]
    public async Task MakeLogViewAdaptorTest()
    {
        var logViewAdaptorHost = Silo.ServiceProvider.GetRequiredService<ILogViewAdaptorHost<TestLogView,TestLogEntry>>();
        var adaptor = Silo.LogConsistencyProvider.MakeLogViewAdaptor<TestLogView,TestLogEntry>(logViewAdaptorHost, new TestLogView(), "", 
            Silo.TestGrainStorage, Silo.ProtocolServices);
        var exception = await Assert.ThrowsAsync<InvalidCastException>(() => adaptor.RetrieveLogSegment(0, 10));
        exception.Message.ShouldContain("Unable to cast object");
    }
    
    public class TestLogView
    {
    }
    
    public class TestLogEntry
    {
    }

    private async Task<bool> CheckCount(LogViewAdaptorTestGAgent gAgent, int expectedCount)
    {
        var state = await gAgent.GetStateAsync();
        return state.Content.Count == expectedCount;
    }

    private string GetStreamName(GrainId grainId)
    {
        return $"Aevatar/EventSourcingTest/log/{grainId.ToString()}";
    }
}