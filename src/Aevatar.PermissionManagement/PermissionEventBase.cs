using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

public interface IPermissionEvent
{
    UserContext? UserContext { get; set; }
}

[GenerateSerializer]
public abstract class PermissionEventBase : EventBase, IPermissionEvent
{
    [Id(0)] public UserContext? UserContext { get; set; } = new();
}