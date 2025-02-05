using Aevatar.Core;
using Aevatar.Core.Abstractions;
using MessagingGAgent.Grains.Agents.Events;
using Microsoft.Extensions.Logging;

namespace MessagingGAgent.Grains.Agents.Messaging;

public interface IMessagingGAgent : IGAgent
{
    Task<int> GetReceivedMessagesAsync();
}

public class MessagingGAgent : GAgentBase<MessagingGState, MessagingStateLogEvent>, IMessagingGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Agent for messaging.");
    }
    
    [EventHandler]
    public async Task OnSendEvent(SendEvent @event)
    {
        await Task.Delay(1000);
        await PublishAsync(new MessagingEvent()
        {
            Message = $"{this.GetGrainId().ToString()} sent a message."
        });
    }

    [EventHandler]
    public async Task OnMessagingEvent(MessagingEvent @event)
    {
        RaiseEvent(new MessagingStateLogEvent());
        await ConfirmEvents();
    }

    public Task<int> GetReceivedMessagesAsync()
    {
        return Task.FromResult(State.ReceivedMessages);
    }
}