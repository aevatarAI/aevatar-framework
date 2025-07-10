using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.EventSourcing;
using Orleans.Runtime;
using Orleans.Serialization;

namespace Aevatar.EventSourcing.Core.LogConsistency;

public class DefaultProtocolServices : ILogConsistencyProtocolServices
{
    private readonly ILogger? _logger;
    private readonly DeepCopier _deepCopier;
    private readonly IGrainContext _grainContext; // links to the grain that owns this service object

    /// <summary>
    /// </summary>
    /// <param name="grainContext"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="deepCopier"></param>
    /// <param name="siloDetails"></param>
    public DefaultProtocolServices(IGrainContext grainContext, ILoggerFactory loggerFactory, DeepCopier deepCopier,
        ILocalSiloDetails? siloDetails)
    {
        _grainContext = grainContext;
        _logger = loggerFactory.CreateLogger<DefaultProtocolServices>();
        _deepCopier = deepCopier;
        MyClusterId = siloDetails?.ClusterId ?? "Unknown";
    }

    /// <inheritdoc />
    public GrainId GrainId => _grainContext.GrainId;

    /// <inheritdoc />
    public string MyClusterId { get; }

    /// <inheritdoc />
    public T DeepCopy<T>(T value)
    {
        return _deepCopier.Copy(value);
    }

    /// <inheritdoc />
    public void ProtocolError(string msg, bool throwexception)
    {
        _logger?.LogError(
            (int)(throwexception
                ? ErrorCode.LogConsistency_ProtocolFatalError
                : ErrorCode.LogConsistency_ProtocolError), "{GrainId} Protocol Error: {Message}", _grainContext.GrainId,
            msg);
        if (!throwexception)
        {
            return;
        }

        throw new OrleansException($"{msg} (grain={_grainContext.GrainId}, cluster={MyClusterId})");
    }

    /// <inheritdoc />
    public void CaughtException(string where, Exception ex)
    {
        _logger?.LogError((int)ErrorCode.LogConsistency_CaughtException, ex, "{GrainId} exception caught at {Location}",
            _grainContext.GrainId, where);
    }

    /// <inheritdoc />
    public void CaughtUserCodeException(string callback, string where, Exception ex)
    {
        _logger?.LogWarning((int)ErrorCode.LogConsistency_UserCodeException, ex,
            "{GrainId} exception caught in user code for {Callback}, called from {Location}", _grainContext.GrainId,
            callback, where);
    }

    /// <inheritdoc />
    public void Log(LogLevel level, string format, params object[] args)
    {
        try
        {
            var message = args?.Length > 0 ? string.Format(format, args) : format;
        
            if (_logger != null)
            {
                _logger.Log(level, message);
            }
            else
            {
               
                Console.WriteLine($"[{level}] {message}");
            }
        }
        catch (FormatException)
        {
           
            var safeArgs = args?.Select(arg => arg?.ToString() ?? "null").ToArray() ?? Array.Empty<string>();
            var safeMessage = $"[FORMAT ERROR] {format} | Args: [{string.Join(", ", safeArgs)}]";
        
            if (_logger != null)
            {
                _logger.Log(level, safeMessage);
            }
            else
            {
                Console.WriteLine($"[{level}] {safeMessage}");
            }
        }
    }
}
