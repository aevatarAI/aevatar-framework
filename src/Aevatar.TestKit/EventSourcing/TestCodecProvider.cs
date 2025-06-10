using System.Collections.Concurrent;
using Orleans.Serialization.Codecs;
using Orleans.Serialization.Cloning;
using Orleans.Serialization.Serializers;
using Orleans.Serialization.Activators;

public sealed class TestCodecProvider : ICodecProvider
{
    private readonly ConcurrentDictionary<Type, object> _codecs = new();
    private readonly ConcurrentDictionary<Type, object> _cloners = new();

    public IFieldCodec<TField> GetCodec<TField>()
    {
        return (IFieldCodec<TField>)_codecs.GetOrAdd(typeof(TField), _ => new TestCodec<TField>());
    }

    public IFieldCodec<TField> TryGetCodec<TField>()
    {
        return GetCodec<TField>();
    }

    public IFieldCodec GetCodec(Type fieldType)
    {
        return (IFieldCodec)_codecs.GetOrAdd(fieldType,
            _ => Activator.CreateInstance(typeof(TestCodec<>).MakeGenericType(fieldType)));
    }

    public IFieldCodec TryGetCodec(Type fieldType)
    {
        return GetCodec(fieldType);
    }

    public IBaseCodec<TField> GetBaseCodec<TField>() where TField : class
    {
        throw new NotImplementedException();
    }

    public IValueSerializer<TField> GetValueSerializer<TField>() where TField : struct
    {
        throw new NotImplementedException();
    }

    public IActivator<T> GetActivator<T>()
    {
        throw new NotImplementedException();
    }

    public IDeepCopier<T> GetDeepCopier<T>()
    {
        return (IDeepCopier<T>)_cloners.GetOrAdd(typeof(T), _ => new TestCloner<T>());
    }

    public IDeepCopier<T> TryGetDeepCopier<T>()
    {
        return GetDeepCopier<T>();
    }

    public IDeepCopier GetDeepCopier(Type type)
    {
        return (IDeepCopier)_cloners.GetOrAdd(type,
            _ => Activator.CreateInstance(typeof(TestCloner<>).MakeGenericType(type)));
    }

    public IDeepCopier TryGetDeepCopier(Type type)
    {
        return GetDeepCopier(type);
    }

    public IBaseCopier<T> GetBaseCopier<T>() where T : class
    {
        throw new NotImplementedException();
    }

    public IServiceProvider Services { get; }
}