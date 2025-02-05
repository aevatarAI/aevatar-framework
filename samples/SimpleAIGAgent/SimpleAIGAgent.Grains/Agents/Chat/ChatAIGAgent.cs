using Aevatar.AI.Agent;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.Logging;
using SimpleAIGAgent.Grains.Agents.Events;

namespace SimpleAIGAgent.Grains.Agents.Chat;

public interface IChatAIGAgent : IAIGAgent, IGAgent
{
    Task<string> ChatAsync(string message);
}

public class ChatAIGAgent : AIGAgentBase<ChatAIGStateBase, ChatAIStateLogEvent>, IChatAIGAgent
{
    public override Task<string> GetDescriptionAsync()
    {
        return Task.FromResult("Agent for chatting with user.");
    }

    public async Task<string> ChatAsync(string message)
    {
        return await InvokePromptAsync(message) ?? string.Empty;
    }

    [EventHandler]
    public async Task OnChatAIEvent(ChatEvent @event)
    {
        var result = await InvokePromptAsync(@event.Message);
        Logger.LogInformation("Chat output: {Result}", result);
    }
}