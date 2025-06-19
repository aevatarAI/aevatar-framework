using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

[GenerateSerializer]
public abstract class PermissionEventBase : EventBase
{
    [Id(2)] public UserContext? UserContext { get; set; }
}