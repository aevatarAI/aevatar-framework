using Aevatar.Core;
using Orleans.Metadata;

namespace Aevatar;

public class ConfigureAevatarGrainActivator : IConfigureGrainTypeComponents
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GrainClassMap _grainClassMap;

    public ConfigureAevatarGrainActivator(GrainClassMap grainClassMap, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _grainClassMap = grainClassMap;
    }

    public void Configure(GrainType grainType, GrainProperties properties, GrainTypeSharedContext shared)
    {
        if (!_grainClassMap.TryGetGrainClass(grainType, out var grainClass))
        {
            return;
        }

        var instanceActivator = new AevatarGrainActivator(_serviceProvider, grainClass);
        shared.SetComponent<IGrainActivator>(instanceActivator);
    }
}