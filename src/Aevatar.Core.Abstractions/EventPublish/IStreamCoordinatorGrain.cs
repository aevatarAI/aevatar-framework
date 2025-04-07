using Orleans.Concurrency;

namespace Aevatar.Core.Abstractions.EventPublish;

public interface IStreamCoordinatorGrain : IGrainWithStringKey
{
    Task<bool> SetParentAsync(GrainId parentGrainId);
    Task ClearParentAsync(GrainId parentGrainId);
    Task SetGroupIndexAsync(int groupIndex);
    [ReadOnly]
    Task<int> GetGroupIndexAsync();
    [ReadOnly]
    Task<GrainId> GetParentAsync();
    Task<int> RegisterChildAsync(GrainId childGrainId);
    Task<int> RegisterManyChildAsync(List<GrainId> childrenGrainIds);
    Task UnregisterChildAsync(GrainId childGrainId);
    [ReadOnly]
    Task<List<GrainId>> GetChildrenAsync();
    Task PublishEventAsync(EventWrapperBase eventWrapper);
    Task DownwardsEventAsync(EventWrapperBase eventWrapper);
    Task UpwardsEventAsync(EventWrapperBase eventWrapper);
}