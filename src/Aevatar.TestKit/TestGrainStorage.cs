using Orleans.Storage;
using Aevatar.TestKit.Storage;

namespace Aevatar.TestKit;

public class TestGrainStorage : IGrainStorage
{
    private readonly StorageManager _storageManager;

    public TestGrainStorage(StorageManager storageManager)
    {
        _storageManager = storageManager;
    }

    public Task ReadStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        stateName = $"{grainId.ToString()}.{stateName}";
        var storage = _storageManager.GetStorage<T>(stateName);
        grainState.State = storage.State;
        return Task.CompletedTask;
    }

    public Task WriteStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        stateName = $"{grainId.ToString()}.{stateName}";
        var storage = _storageManager.GetStorage<T>(stateName);
        storage.State = grainState.State;
        _storageManager.AddStorage(storage, stateName);
        return Task.CompletedTask;
    }

    public Task ClearStateAsync<T>(string stateName, GrainId grainId, IGrainState<T> grainState)
    {
        stateName = $"{grainId.ToString()}.{stateName}";
        var storage = _storageManager.GetStorage<T>(stateName);
        storage.State = default!;
        return Task.CompletedTask;
    }
}