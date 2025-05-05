namespace Aevatar.Core.Abstractions.EventPublish;

using Orleans.Concurrency;

public interface IEventPubChildrenGroupGrain : IGrainWithIntegerCompoundKey
{
    Task DownwardsEventAsync(EventWrapperBase eventWrapper);
    Task UpwardsEventAsync(EventWrapperBase eventWrapper);
    Task AddChildAsync(GrainId childGrainId);
    Task AddManyChildAsync(List<GrainId> childrenGrainIds);
    Task RemoveChildAsync(GrainId childId);
    [ReadOnly]
    Task<List<GrainId>> GetChildrenAsync();
    [ReadOnly]
    Task<int> GetChildrenCountAsync();
}