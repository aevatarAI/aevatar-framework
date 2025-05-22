namespace Aevatar.Core.Abstractions.Plugin;

/// <summary>
/// Plugin DLL load status information.
/// </summary>
public class PluginLoadStatus
{
    /// <summary>
    /// Load status: Success or Failed.
    /// </summary>
    public LoadStatus Status { get; set; }

    /// <summary>
    /// Failure reason (only set if Status is Failed).
    /// </summary>
    public string? Reason { get; set; }
}

public enum LoadStatus
{
    Unload = -1,
    Success = 0,
    GAgentDuplicated,
    AlreadyLoaded,
    Error
}