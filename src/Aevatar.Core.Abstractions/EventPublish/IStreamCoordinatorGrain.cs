namespace Aevatar.Core.Abstractions.EventPublish;

public interface IStreamCoordinatorGrain : IGrainWithStringKey
{
    Task<bool> SetParentAsync(GrainId parentGrainId);
    Task ClearParentAsync(GrainId parentGrainId);
    Task SetGroupIndexAsync(int groupIndex);
    Task<int> GetGroupIndexAsync();
    Task<GrainId> GetParentAsync();
    Task<int> RegisterChildAsync(GrainId childGrainId);
    Task RegisterManyChildAsync(List<GrainId> childrenGrainIds);
    Task UnregisterChildAsync(GrainId childGrainId);
    Task<List<GrainId>> GetChildrenAsync();
    Task PublishEventAsync(EventWrapperBase eventWrapper);
    Task DownwardsEventAsync(EventWrapperBase eventWrapper);
    Task UpwardsEventAsync(EventWrapperBase eventWrapper);
}