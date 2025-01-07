namespace Aevatar.Core.Abstractions;

[GenerateSerializer]
public abstract class StateBase
{
    [Id(0)] public List<GrainId> Subscribers { get; set; } = [];
    [Id(1)] public GrainId Subscription { get; set; }

    public void Apply(AddSubscriberGEvent addSubscriber)
    {
        if (!Subscribers.Contains(addSubscriber.Subscriber))
        {
            Subscribers.Add(addSubscriber.Subscriber);
        }
    }
    
    public void Apply(RemoveSubscriberGEvent removeSubscriber)
    {
        Subscribers.Remove(removeSubscriber.Subscriber);
    }
    
    public void Apply(SetSubscriptionGEvent setSubscription)
    {
        Subscription = setSubscription.Subscription;
    }
}