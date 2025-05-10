namespace Aevatar.Agent.Abstractions;

public interface IGAgentState 
{
    List<GrainId> Children{get;set;}
    GrainId? Parent{get;set;}
    string? GAgentCreator{get;set;}
    
}
