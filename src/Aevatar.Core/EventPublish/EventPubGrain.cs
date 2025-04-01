using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.EventPublish;
using Aevatar.Core.Extensions;
using Orleans.Streams;

namespace Aevatar.Core.EventPublish;

public class EventPubGrain : Grain, IEventPubGrain
{
    private readonly IStreamProvider _streamProvider;

    public EventPubGrain()
    {
        _streamProvider = this.GetStreamProvider(AevatarCoreConstants.StreamProvider);
    }

    public async Task PublishEventAsync(EventWrapperBase eventWrapper)
    {
        var stream = _streamProvider.GetEventWrapperBaseStream(this.GetPrimaryKeyString());
        await stream.OnNextAsync(eventWrapper);
        Called.Add(this.GetPrimaryKeyString());
    }

    public static List<string> Called = [];
}