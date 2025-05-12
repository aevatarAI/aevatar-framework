using System.Reflection;

namespace Aevatar.Core.Abstractions.Extensions;

public static class AssemblyExtensions
{
    public static Type[] GetTypesIgnoringLoadException(this Assembly? assembly)
    {
        if (assembly is null) return [];
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        return types;
    }
}