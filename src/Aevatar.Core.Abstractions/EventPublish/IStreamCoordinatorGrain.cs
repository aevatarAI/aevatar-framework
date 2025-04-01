namespace Aevatar.Core.Abstractions.EventPublish;

public interface IStreamCoordinatorGrain : IGrainWithStringKey
{
    Task<bool> SetParentAsync(GrainId parentGrainId);
    Task<GrainId> GetParentAsync();
    Task RegisterChildAsync(GrainId childGrainId);
    Task UnregisterChildAsync(GrainId childGrainId);
    Task<List<GrainId>> GetChildrenAsync();
    Task PublishEventAsync(EventWrapperBase eventWrapper);
    Task DownwardsEventAsync(EventWrapperBase eventWrapper);
    Task UpwardsEventAsync(EventWrapperBase eventWrapper);
}