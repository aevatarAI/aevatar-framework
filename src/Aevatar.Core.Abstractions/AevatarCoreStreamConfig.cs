namespace Aevatar.Core.Abstractions;

public static class AevatarCoreStreamConfig
{
    public static string Prefix { get; private set; } = AevatarCoreConstants.DefaultStreamNamespace;

    public static void Initialize(string? prefix)
    {
        if (prefix != null)
        {
            Prefix = prefix;
        }
    }
}