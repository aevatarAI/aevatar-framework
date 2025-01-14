using System.Reflection;
using System.Runtime.Loader;

namespace Aevatar.ProxyGAgent;

public class ProxyCodeLoadContext : AssemblyLoadContext
{
    private readonly ISdkStreamManager _sdkStreamManager;

    public ProxyCodeLoadContext(ISdkStreamManager sdkStreamManager) : base(true)
    {
        _sdkStreamManager = sdkStreamManager;
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        return LoadFromFolderOrDefault(assemblyName);
    }

    private Assembly LoadFromFolderOrDefault(AssemblyName assemblyName)
    {
        if (assemblyName.Name.StartsWith("Aevatar.ProxyGAgent.Sdk"))
        {
            // Sdk assembly should NOT be shared
            using var stream = _sdkStreamManager.GetStream(assemblyName);
            return LoadFromStream(stream);
        }

        return null;
    }
}