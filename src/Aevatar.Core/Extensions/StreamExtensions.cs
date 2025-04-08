using Aevatar.Core.Abstractions;
using Orleans.Streams;

namespace Aevatar.Core.Extensions;

public static class StreamExtensions
{
    public static IAsyncStream<EventWrapperBase> GetEventWrapperBaseStream(this IStreamProvider streamProvider,
        string streamKey)
        => streamProvider.GetStream<EventWrapperBase>(StreamId.Create(AevatarCoreStreamConfig.Prefix, streamKey));

    public static IAsyncStream<EventWrapperBase> GetEventWrapperBaseStream(this IStreamProvider streamProvider,
        GrainId grainId)
        => streamProvider.GetStream<EventWrapperBase>(StreamId.Create(AevatarCoreStreamConfig.Prefix,
            grainId.ToString()));
}