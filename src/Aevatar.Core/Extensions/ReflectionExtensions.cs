using System.Reflection;

namespace Aevatar.Core.Extensions;

internal static class ReflectionExtensions
{
    public static bool HasAttribute<T>(this MethodInfo method) where T : Attribute =>
        method.GetCustomAttribute<T>() != null;
}
