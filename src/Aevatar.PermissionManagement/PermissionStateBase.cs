using System;
using System.Collections.Generic;
using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

public interface IPermissionStateBase
{
    bool IsPublic { get; set; }
   HashSet<Guid> AuthorizedUserIds { get; set; }
}

[GenerateSerializer]
public abstract class PermissionStateBase : StateBase, IPermissionStateBase
{
    [Id(0)] public bool IsPublic { get; set; } = true;
    [Id(1)] public HashSet<Guid> AuthorizedUserIds { get; set; } = new();
}