using Aevatar.Core;
using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

public abstract partial class
    PermissionGAgentBase<TState, TStateLogEvent> : PermissionGAgentBase<TState, TStateLogEvent, EventBase, ConfigurationBase>
    where TState : PermissionStateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
{
}

public abstract partial class
    PermissionGAgentBase<TState, TStateLogEvent, TEvent> : PermissionGAgentBase<TState, TStateLogEvent, TEvent, ConfigurationBase>
    where TState : PermissionStateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
{
}

public abstract partial class
    PermissionGAgentBase<TState, TStateLogEvent, TEvent, TConfiguration> :
    GAgentBase<TState, TStateLogEvent, TEvent, TConfiguration>
    where TState : PermissionStateBase, new()
    where TStateLogEvent : StateLogEventBase<TStateLogEvent>
    where TEvent : EventBase
    where TConfiguration : ConfigurationBase
{
    protected async Task AddAuthorizedUsersAsync(params Guid[] userIds)
    {
        RaiseEvent(new AddAuthorizedUsersLogEventBase
        {
            AuthorizedUserIds = userIds.ToList()
        });
        await ConfirmEvents();
    }
    
    protected async Task RemoveAuthorizedUsersAsync(params Guid[] userIds)
    {
        RaiseEvent(new RemoveAuthorizedUsersLogEvent
        {
            AuthorizedUserIds = userIds.ToList()
        });
        await ConfirmEvents();
    }


    protected sealed override void GAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
        switch (@event)
        {
            case AddAuthorizedUsersLogEventBase addAuthorizedUsers:
                foreach (var userId in addAuthorizedUsers.AuthorizedUserIds)
                {
                    State.AuthorizedUserIds.Add(userId);
                }
                State.IsPublic = State.AuthorizedUserIds.Count == 0;
                break;
            case RemoveAuthorizedUsersLogEvent removeAuthorizedUsers:
                foreach (var userId in removeAuthorizedUsers.AuthorizedUserIds)
                {
                    State.AuthorizedUserIds.Remove(userId);
                }
                State.IsPublic = State.AuthorizedUserIds.Count == 0;
                break;
        }

        PermissionGAgentTransitionState(state, @event);
    }
    
    protected virtual void PermissionGAgentTransitionState(TState state, StateLogEventBase<TStateLogEvent> @event)
    {
    }
    
    [GenerateSerializer]
    public abstract class SetAuthorizedUsersLogEventBase : StateLogEventBase<TStateLogEvent>
    {
        [Id(0)] public List<Guid> AuthorizedUserIds { get; set; } = new();
    }
    
    [GenerateSerializer]
    public class AddAuthorizedUsersLogEventBase : SetAuthorizedUsersLogEventBase
    {
    }
    
    [GenerateSerializer]
    public class RemoveAuthorizedUsersLogEvent : SetAuthorizedUsersLogEventBase
    {
    }
}