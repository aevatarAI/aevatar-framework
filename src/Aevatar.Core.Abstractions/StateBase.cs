namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class StateBase
{
    [Id(0)] public List<GrainId> Children { get; set; } = [];
    [Id(1)] public GrainId Parent { get; set; }
    [Id(2)] public Type? InitializeDtoType { get; set; }

    /*
    public void Apply(AddChildStateLogEvent addChild)
    {
        if (!Children.Contains(addChild.Child))
        {
            Children.Add(addChild.Child);
        }
    }
    
    public void Apply(RemoveChildStateLogEvent removeChild)
    {
        Children.Remove(removeChild.Child);
    }
    
    public void Apply(SetParentStateLogEvent setParent)
    {
        Parent = setParent.Parent;
    }

    public void Apply(SetInitializeDtoTypeStateLogEvent setInitializeDtoType)
    {
        InitializeDtoType = setInitializeDtoType.InitializeDtoType;
    }*/
}