using Aevatar.Core.Abstractions;

namespace Aevatar.Core;

public class EventPubGrain : Grain, IEventPubGrain
{
    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var parentGrainId = this.GetGrainId().GetGuidKey();
        return base.OnActivateAsync(cancellationToken);
    }
}