namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public class StateWrapper<T>(GrainId grainId, T state, int version) : StateWrapperBase
    where T : StateBase
{
    [Id(0)] public GrainId GrainId { get; private set; } = grainId;
    [Id(1)] public T State { get; private set; } = state;
    [Id(2)] public int Version { get; private set; } = version;
}