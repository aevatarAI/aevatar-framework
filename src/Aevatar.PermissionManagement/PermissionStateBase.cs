using System;
using System.Collections.Generic;
using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

[GenerateSerializer]
public class PermissionStateBase : StateBase
{
    [Id(0)]
    public bool IsPublic { get; set; } = true;
    [Id(1)]
    public HashSet<Guid> AuthorizedUserIds { get; set; } = new();
} 