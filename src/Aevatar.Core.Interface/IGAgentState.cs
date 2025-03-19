namespace Aevatar.Core.Interface;

public interface IGAgentState 
{
    List<GrainId> Children{get;set;}
    GrainId? Parent{get;set;}
    string? GAgentCreator{get;set;}
    
}
