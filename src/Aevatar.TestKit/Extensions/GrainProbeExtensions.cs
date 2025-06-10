using Moq;

namespace Aevatar.TestKit.Extensions;

public static class GrainProbeExtensions
{
    public static Mock<T> AddProbe<T>(this TestKitSilo silo, long id, string? classPrefix = null)
        where T : class, IGrainWithIntegerKey
    {
        ArgumentNullException.ThrowIfNull(silo);

        return silo.GrainFactory.AddProbe<T>(GrainIdKeyExtensions.CreateIntegerKey(id), classPrefix);
    }

    public static Mock<T> AddProbe<T>(this TestKitSilo silo, Guid id, string? classPrefix = null)
        where T : class, IGrainWithGuidKey
    {
        ArgumentNullException.ThrowIfNull(silo);

        return silo.GrainFactory.AddProbe<T>(GrainIdKeyExtensions.CreateGuidKey(id), classPrefix);
    }

    public static Mock<T> AddProbe<T>(this TestKitSilo silo, Guid id, string? classPrefix, Func<IdSpan, T> factory)
        where T : class, IGrainWithGuidKey
    {
        ArgumentNullException.ThrowIfNull(silo);
        return silo.GrainFactory.AddProbe<T>(GrainIdKeyExtensions.CreateGuidKey(id), classPrefix);
    }

    public static Mock<T> AddProbe<T>(this TestKitSilo silo, long id, string keyExtension, string? classPrefix = null)
        where T : class, IGrainWithIntegerCompoundKey
    {
        ArgumentNullException.ThrowIfNull(silo);

        return silo.GrainFactory.AddProbe<T>(GrainIdKeyExtensions.CreateIntegerKey(id, keyExtension), classPrefix);
    }

    public static Mock<T> AddProbe<T>(this TestKitSilo silo, Guid id, string keyExtension, string? classPrefix = null)
        where T : class, IGrainWithGuidCompoundKey
    {
        ArgumentNullException.ThrowIfNull(silo);

        return silo.GrainFactory.AddProbe<T>(GrainIdKeyExtensions.CreateGuidKey(id, keyExtension), classPrefix);
    }

    public static Mock<T> AddProbe<T>(this TestKitSilo silo, string id, string? classPrefix = null)
        where T : class, IGrainWithStringKey
    {
        ArgumentNullException.ThrowIfNull(silo);

        return silo.GrainFactory.AddProbe<T>(IdSpan.Create(id), classPrefix);
    }

    public static void AddProbe<T>(this TestKitSilo silo, Func<IdSpan, IMock<T>> factory)
        where T : class, IGrain
    {
        ArgumentNullException.ThrowIfNull(silo);

        silo.GrainFactory.AddProbe(factory);
    }

    public static void AddProbe<T>(this TestKitSilo silo, Func<IdSpan, T> factory)
        where T : class, IGrain
    {
        ArgumentNullException.ThrowIfNull(silo);

        silo.GrainFactory.AddProbe(factory);
    }
    
    public static void AddProbe<T>(this TestKitSilo silo, GrainId grainId, T grain)
        where T : class, IGrain
    {
        silo.GrainFactory.AddProbe(grainId, grain);
    }
}
