namespace Aevatar.Core.Abstractions.EventPublish;

public interface IEventPubChildrenGroupGrain : IGrainWithIntegerCompoundKey
{
    Task DownwardsEventAsync(EventWrapperBase eventWrapper);
    Task UpwardsEventAsync(EventWrapperBase eventWrapper);
    Task AddChildAsync(GrainId childId);
    Task RemoveChildAsync(GrainId childId);
    Task<List<GrainId>> GetChildrenAsync();
    Task<int> GetChildrenCountAsync();
}