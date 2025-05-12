using System.Reflection;
using Aevatar.Core.Abstractions;

namespace Aevatar.Core.Extensions;

internal static class MethodInfoExtensions
{
    internal static (Type, bool) AnalysisMethodMetadata(this MethodInfo method)
    {
        var paramInfo = method.GetParameters()[0];
        var paramType = paramInfo.ParameterType;
        var isResponse = paramInfo.ParameterType.BaseType is { IsGenericType: true } &&
                         paramInfo.ParameterType.BaseType.GetGenericTypeDefinition() == typeof(EventWithResponseBase<>);
        return (paramType, isResponse);
    }
    
    internal static bool IsSelfHandlingAllowed(this MethodInfo method)
    {
        return (method.GetCustomAttribute<EventHandlerAttribute>()?.AllowSelfHandling ??
                method.GetCustomAttribute<AllEventHandlerAttribute>()?.AllowSelfHandling) ?? false;
    }
}