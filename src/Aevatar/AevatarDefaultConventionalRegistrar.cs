using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.DependencyInjection;

namespace Aevatar;

public class AevatarDefaultConventionalRegistrar : DefaultConventionalRegistrar
{
    private readonly List<string> _transientTypeSuffixes =
        ["Service", "Provider", "Manager", "Factory", "GAgent"];

    protected override ServiceLifetime? GetServiceLifetimeFromClassHierarchy(Type type)
    {
        // Get ABP lifetime from ABP interface, ITransientDependency, ISingletonDependency or IScopedDependency
        var lifeTime = base.GetServiceLifetimeFromClassHierarchy(type);
        if (lifeTime != null)
        {
            return null;
        }

        // If no lifetime interface was found, try to get class with the same interface,
        // HelloService -> IHelloService
        // HelloManager -> IHelloManager
        var interfaceName = "I" + type.Name;

        if (type.GetInterfaces().Any(p => p.Name == interfaceName))
            if (_transientTypeSuffixes.Any(suffix => type.Name.EndsWith(suffix)))
                return ServiceLifetime.Transient;

        return null;
    }
}