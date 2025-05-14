using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Core.Abstractions.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Metadata;
using Volo.Abp.Threading;

namespace Aevatar.Core;

public class GAgentManager : IGAgentManager
{
    private readonly IPluginGAgentManager _pluginGAgentManager;
    private readonly GrainTypeResolver _grainTypeResolver;

    public GAgentManager(IClusterClient clusterClient, IPluginGAgentManager pluginGAgentManager)
    {
        _pluginGAgentManager = pluginGAgentManager;
        _grainTypeResolver = clusterClient.ServiceProvider.GetRequiredService<GrainTypeResolver>();
    }

    public List<Type> GetAvailableGAgentTypes()
    {
        var gAgentType = typeof(IGAgent);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var pluginsAssemblies = AsyncHelper.RunSync(() => _pluginGAgentManager.GetCurrentTenantPluginAssembliesAsync());
        var pluginsLoadStatus = AsyncHelper.RunSync(() => _pluginGAgentManager.GetPluginLoadStatusAsync());
        var loadedFailedAssemblies = pluginsLoadStatus
            .Where(x => x.Value.Status != LoadStatus.Success)
            .Select(x => x.Key.Split('_').First())
            .ToList();
        var uniqueAssemblies = new Dictionary<string, Assembly>();
        foreach (var assembly in assemblies)
        {
            uniqueAssemblies.TryAdd(assembly.FullName!, assembly);
        }
        assemblies = uniqueAssemblies.Values.ToList();
    
        foreach (var assembly in pluginsAssemblies)
        {
            if (loadedFailedAssemblies.Contains(assembly.FullName!))
            {
                assemblies.RemoveAll(a => a.FullName == assembly.FullName);
            }
        }

        var gAgentTypes = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypesIgnoringLoadException()
                .Where(t => gAgentType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
            gAgentTypes.AddRange(types);
        }

        return gAgentTypes;
    }

    public List<Type> GetAvailableEventTypes()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
        var pluginsAssemblies = AsyncHelper.RunSync(() => _pluginGAgentManager.GetCurrentTenantPluginAssembliesAsync());
        assemblies.AddIfNotContains(pluginsAssemblies);
        var eventTypes = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypesIgnoringLoadException()
                .Where(t => t.IsSubclassOf(typeof(EventBase)) && t is { IsClass: true, IsAbstract: false });
            eventTypes.AddRange(types);
        }

        return eventTypes;
    }

    public List<GrainType> GetAvailableGAgentGrainTypes()
    {
        var types = GetAvailableGAgentTypes();
        var grainTypes = types.Select(_grainTypeResolver.GetGrainType);
        return grainTypes.ToList();
    }
}