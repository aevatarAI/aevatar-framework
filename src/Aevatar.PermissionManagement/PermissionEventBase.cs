using Aevatar.Core.Abstractions;

namespace Aevatar.PermissionManagement;

/// <summary>
/// Base class for permission-related events that carry user context information.
/// This class provides a foundation for events that need to propagate user context
/// throughout the permission management system.
/// </summary>
[GenerateSerializer]
public abstract class PermissionEventBase : EventBase
{
    /// <summary>
    /// Gets or sets the user context associated with this permission event.
    /// This context contains user identification, roles, and other security-related information.
    /// </summary>
    [Id(2)] public UserContext? UserContext { get; set; }

    /// <summary>
    /// Initializes a new instance of the PermissionEventBase class.
    /// </summary>
    protected PermissionEventBase()
    {
    }

    /// <summary>
    /// Initializes a new instance of the PermissionEventBase class with the specified user context.
    /// </summary>
    /// <param name="userContext">The user context to associate with this event.</param>
    protected PermissionEventBase(UserContext? userContext)
    {
        UserContext = userContext;
    }

    /// <summary>
    /// Gets a value indicating whether this event has a valid user context.
    /// </summary>
    public bool HasUserContext => UserContext != null;

    /// <summary>
    /// Gets the user ID from the user context, or null if no context is available.
    /// </summary>
    public Guid? UserId => UserContext?.UserId;

    /// <summary>
    /// Gets the user roles from the user context, or an empty array if no context is available.
    /// </summary>
    public string[] UserRoles => UserContext?.Roles ?? Array.Empty<string>();
} 