using System.Collections.Concurrent;
using System.Reflection;

namespace Aevatar.ProxyGAgent;

public interface ISdkStreamManager
{
    Stream GetStream(AssemblyName assemblyName);
}

public class SdkStreamManager(string sdkDir) : ISdkStreamManager
{
    private readonly ConcurrentDictionary<string, byte[]> _cachedSdkStreams = new();

    public Stream GetStream(AssemblyName assemblyName)
    {
        var path = Path.Combine(sdkDir, assemblyName.Name + ".dll");
        if (!File.Exists(path))
        {
            var assembly = Assembly.Load(assemblyName);

            path = assembly.Location;
        }

        if (!_cachedSdkStreams.TryGetValue(path, out var buffer))
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var length = (int)fs.Length;
            buffer = new byte[length];
            fs.ReadExactly(buffer, 0, length);
            _cachedSdkStreams.TryAdd(path, buffer);
        }

        return new MemoryStream(buffer);
    }
}