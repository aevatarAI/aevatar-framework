namespace Aevatar.Core;

public static class AevatarGAgentConstants
{
    public const string EventHandlerDefaultMethodName = "HandleEventAsync";
    public const string StateHandlerDefaultMethodName = "HandleStateAsync";
    public const string ConfigDefaultMethodName = "PerformConfigAsync";
    public const string ForwardEventMethodName = "ForwardEventAsync";
    public const int MaxSyncWorkConcurrency = 4;
}