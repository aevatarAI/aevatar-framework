using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aevatar.AI.Brain;
using Aevatar.AI.BrainFactory;
using Aevatar.AI.Dtos;
using Aevatar.AI.State;
using Aevatar.Core;
using Aevatar.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;

namespace Aevatar.AI.Agent;

public abstract class AIGAgentBase<TState, TStateLogEvent> : GAgentBase<TState, TStateLogEvent>, IAIGAgent
    where TState : AIGAgentStateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
{
    private readonly IBrainFactory _brainFactory;
    private IBrain? _brain = null;
    
    public AIGAgentBase()
    {
        _brainFactory = ServiceProvider.GetRequiredService<IBrainFactory>();
    }

    public async Task<bool> InitializeAsync(InitializeDto initializeDto)
    {
        //save state
        await AddLLMAsync(initializeDto.LLM);
        await AddPromptTemplateAsync(initializeDto.Instructions);
        
        return await InitializeBrainAsync(initializeDto.LLM, initializeDto.Instructions, initializeDto.Files);
    }

    private async Task<bool> InitializeBrainAsync(string LLM, string promptTemplate, IReadOnlyCollection<FileDto>? files = null)
    {
        _brain = _brainFactory.GetBrain(LLM);
        
        if(_brain == null)
        {
            Logger.LogError("Failed to initialize brain. {@LLM}", LLM);
            return false;
        }

        List<FileData>? fileList = null;
        if (files != null)
        {
            fileList = files.Select(f => new FileData() { Content = f.Content, Type = f.Type, Name = f.Name }).ToList();
        }
        
        // remove slash from this.GetGrainId().ToString() so that it can be used as the collection name pertaining to the grain
        var grainId = this.GetGrainId().ToString().Replace("/", "");
        
        var result = await _brain.InitializeAsync(
            grainId,
            promptTemplate, 
            fileList);

        return result;
    }
    
    private async Task AddLLMAsync(string LLM)
    {
        if (State.LLM == LLM)
        {
            Logger.LogError("Cannot add duplicate LLM: {LLM}.", LLM);
            return;
        }

        RaiseEvent(new SetLLMStateLogEvent
        {
            LLM = LLM
        });
        await ConfirmEvents();
    }
    
    [GenerateSerializer]
    public class SetLLMStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public required string LLM { get; set; }
    }
    
    private async Task AddPromptTemplateAsync(string promptTemplate)
    {
        RaiseEvent(new SetPromptTemplateStateLogEvent
        {
            PromptTemplate = promptTemplate
        });
        await ConfirmEvents();
    }
    
    [GenerateSerializer]
    public class SetPromptTemplateStateLogEvent : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public required string PromptTemplate { get; set; }
    }

    protected async Task<string?> InvokePromptAsync(string prompt)
    {
        return await _brain?.InvokePromptAsync(prompt)!;
    }
    
    protected virtual async Task OnAIGAgentActivateAsync(CancellationToken cancellationToken)
    {
        // Derived classes can override this method.
    }

    protected sealed override async Task OnGAgentActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnGAgentActivateAsync(cancellationToken);
        
        // setup brain
        if(State.LLM != string.Empty)
        {
            await InitializeBrainAsync(State.LLM, State.PromptTemplate);
        }
        
        await OnAIGAgentActivateAsync(cancellationToken);
    }
    
    protected sealed override void GAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        switch (@event)
        {
            case SetLLMStateLogEvent setLlmStateLogEvent:
                State.LLM = setLlmStateLogEvent.LLM;
                break;
            case SetPromptTemplateStateLogEvent setPromptTemplateStateLogEvent:
                State.PromptTemplate = setPromptTemplateStateLogEvent.PromptTemplate;
                break;
        }

        AIGAgentTransitionState(state, @event);
        TransitionState(state, @event);
    }
    
    protected virtual void AIGAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        // Derived classes can override this method.
    }
}