namespace Aevatar.Core.Abstractions;

public class AevatarOptions
{
    public string StreamNamespace { get; set; } = "Aevatar";
    public string StateProjectionStreamNamespace { get; set; } = "AevatarStateProjection";

    public string BroadCastStreamNamespace { get; set; } = "AevatarBroadCast";
    public string ExceptionStreamNamespace { get; set; } = "AevatarDLQ";
    public string ExceptionStreamKey { get; set; } = "Global";

    public int ExceptionStackMaxLength { get; set; } = 1024;
    //public int ElasticSearchProcessors { get; set; } = 10;
}