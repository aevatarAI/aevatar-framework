using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Aevatar.Core.Abstractions;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using Aevatar.Plugins.Repositories;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Aevatar.GAgents.Tests;

public class InMemoryPluginCodeStorageRepository : IPluginCodeStorageRepository
{
    private readonly ConcurrentDictionary<string, PluginCodeStorageSnapshotDocument> _store = new();

    public Task<PluginCodeStorageSnapshotDocument> InsertAsync(PluginCodeStorageSnapshotDocument entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<PluginCodeStorageSnapshotDocument> UpdateAsync(PluginCodeStorageSnapshotDocument entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<PluginCodeStorageSnapshotDocument> GetAsync(string id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<List<PluginCodeStorageSnapshotDocument>> GetListAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public Task<byte[]?> GetPluginCodeByGAgentPrimaryKey(Guid primaryKey)
    {
        var grainTypeName = typeof(Aevatar.Plugins.GAgents.PluginCodeStorageGAgent).FullName!;
        var id = $"{grainTypeName}/{primaryKey:N}";
        if (_store.TryGetValue(id, out var doc))
        {
            return Task.FromResult(doc.Doc.Snapshot.Code?.Value);
        }

        return Task.FromResult<byte[]?>(null);
    }

    public Task<Dictionary<Type, string>> GetPluginDescriptionsByGAgentPrimaryKey(Guid primaryKey)
    {
        var grainTypeName = typeof(PluginCodeStorageGAgent).FullName!;
        var id = $"{grainTypeName}/{primaryKey:N}";
        if (_store.TryGetValue(id, out var doc))
        {
            var entries = doc.Doc.Snapshot.Descriptions;
            return Task.FromResult(entries.ToDictionary(e => Type.GetType(e.Key), e => e.Value));
        }

        return Task.FromResult(new Dictionary<Type, string>());
    }

    public Task<IReadOnlyList<byte[]>> GetPluginCodesByGAgentPrimaryKeys(IReadOnlyList<Guid> primaryKeys)
    {
        var grainTypeName = typeof(Aevatar.Plugins.GAgents.PluginCodeStorageGAgent).FullName!;
        var result = new List<byte[]>();
        foreach (var key in primaryKeys)
        {
            var id = $"{grainTypeName}/{key:N}";
            if (_store.TryGetValue(id, out var doc) && doc.Doc.Snapshot.Code?.Value != null)
            {
                result.Add(doc.Doc.Snapshot.Code.Value);
            }
        }

        return Task.FromResult((IReadOnlyList<byte[]>)result);
    }

    public Task<int> CountAsync()
    {
        return Task.FromResult(_store.Count);
    }

    public Task<bool> AnyAsync(Func<PluginCodeStorageSnapshotDocument, bool> predicate)
    {
        return Task.FromResult(_store.Values.Any(predicate));
    }

    public Task<PluginCodeStorageSnapshotDocument?> FirstOrDefaultAsync(
        Func<PluginCodeStorageSnapshotDocument, bool> predicate)
    {
        return Task.FromResult(_store.Values.FirstOrDefault(predicate));
    }

    public IQueryable<PluginCodeStorageSnapshotDocument> WithDetails()
    {
        throw new NotImplementedException();
    }

    public IQueryable<PluginCodeStorageSnapshotDocument> WithDetails(
        params Expression<Func<PluginCodeStorageSnapshotDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    Task<IQueryable<PluginCodeStorageSnapshotDocument>> IReadOnlyRepository<PluginCodeStorageSnapshotDocument>.
        WithDetailsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<PluginCodeStorageSnapshotDocument>> WithDetailsAsync(
        params Expression<Func<PluginCodeStorageSnapshotDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<PluginCodeStorageSnapshotDocument>> GetQueryableAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<PluginCodeStorageSnapshotDocument>> GetListAsync(
        Expression<Func<PluginCodeStorageSnapshotDocument, bool>> predicate, bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public IAsyncQueryableExecuter AsyncExecuter { get; }

    public Task<List<PluginCodeStorageSnapshotDocument>> WithDetailsAsync()
    {
        // InMemory无导航属性，直接返回全部
        return Task.FromResult(_store.Values.ToList());
    }

    public Task<List<PluginCodeStorageSnapshotDocument>> GetAllListAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public bool? IsChangeTrackingEnabled { get; }

    public Task<List<PluginCodeStorageSnapshotDocument>> GetListAsync(bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<long> GetCountAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<List<PluginCodeStorageSnapshotDocument>> GetPagedListAsync(int skipCount, int maxResultCount,
        string sorting, bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument> InsertAsync(PluginCodeStorageSnapshotDocument entity,
        bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task InsertManyAsync(IEnumerable<PluginCodeStorageSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument> UpdateAsync(PluginCodeStorageSnapshotDocument entity,
        bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task UpdateManyAsync(IEnumerable<PluginCodeStorageSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(PluginCodeStorageSnapshotDocument entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteManyAsync(IEnumerable<PluginCodeStorageSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument?> FindAsync(
        Expression<Func<PluginCodeStorageSnapshotDocument, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument> GetAsync(
        Expression<Func<PluginCodeStorageSnapshotDocument, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Expression<Func<PluginCodeStorageSnapshotDocument, bool>> predicate, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteDirectAsync(Expression<Func<PluginCodeStorageSnapshotDocument, bool>> predicate,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument> GetAsync(string id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginCodeStorageSnapshotDocument?> FindAsync(string id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string id, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteManyAsync(IEnumerable<string> ids, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    private void PerformSyncState(string id, PluginCodeStorageGAgentState state)
    {
        var code = new ByteArrayContainer
        {
            Type = "System.Byte[], System.Private.CoreLib",
            Value = state.Code
        };
        if (_store.TryGetValue(id, out var doc))
        {
            doc.Doc.Snapshot.Code = code;
            doc.Doc.Snapshot.Descriptions = new Dictionary<string, string>(state.Descriptions);
        }
        else
        {
            _store[id] = new PluginCodeStorageSnapshotDocument
            {
                Doc = new PluginCodeStorageDoc
                {
                    Snapshot = new PluginCodeStorageSnapshot
                    {
                        Code = code,
                        Descriptions = new Dictionary<string, string>(state.Descriptions)
                    }
                }
            };
        }
    }

    public async Task SyncStoreAsync(IStateGAgent<PluginCodeStorageGAgentState> codeStorageGAgent)
    {
        var pluginId = codeStorageGAgent.GetPrimaryKey();
        var grainTypeName = typeof(PluginCodeStorageGAgent).FullName!;
        var id = $"{grainTypeName}/{pluginId:N}";
        var state = await codeStorageGAgent.GetStateAsync();
        PerformSyncState(id, state);
    }
}