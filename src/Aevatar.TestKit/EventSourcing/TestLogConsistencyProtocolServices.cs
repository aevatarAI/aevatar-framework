using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;

namespace Aevatar.TestKit;

public class TestLogConsistencyProtocolServices : ILogConsistencyProtocolServices
{
    public T DeepCopy<T>(T value)
    {
        // Implement a simple deep copy logic or use a library like Newtonsoft.Json
        return value; // Simplified for demonstration
    }

    public void ProtocolError(string msg, bool throwexception)
    {
        if (throwexception)
        {
            throw new InvalidOperationException(msg);
        }
        else
        {
            Console.WriteLine($"Protocol Error: {msg}");
        }
    }

    public void CaughtException(string where, Exception e)
    {
        Console.WriteLine($"Exception caught in {where}: {e}");
    }

    public void CaughtUserCodeException(string callback, string where, Exception e)
    {
        Console.WriteLine($"User code exception in {callback} at {where}: {e}");
    }

    public void Log(LogLevel level, string format, params object[] args)
    {
        Console.WriteLine($"Log [{level}]: {string.Format(format, args)}");
    }

    public GrainId GrainId { get; }
    public string MyClusterId { get; } = "TestCluster";
}
