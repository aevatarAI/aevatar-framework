using System.Collections.Concurrent;
using System.Linq.Expressions;
using Aevatar.Core.Abstractions.Plugin;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.Repositories;
using Volo.Abp.Linq;

namespace Aevatar.GAgents.Tests;

public class InMemoryPluginLoadStatusRepository : IPluginLoadStatusRepository
{
    private readonly ConcurrentDictionary<string, PluginLoadStatusDocument> _store = new();

    public Task<PluginLoadStatusDocument> InsertAsync(PluginLoadStatusDocument entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task<PluginLoadStatusDocument> UpdateAsync(PluginLoadStatusDocument entity)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<PluginLoadStatusDocument?> GetAsync(string id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<List<PluginLoadStatusDocument>> GetListAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public async Task<Dictionary<string, PluginLoadStatus>> GetPluginLoadStatusAsync(Guid tenantId)
    {
        var document = await GetAsync(tenantId.ToString("N"));
        return document == null ? new Dictionary<string, PluginLoadStatus>() : document.LoadStatus;
    }

    public async Task SetPluginLoadStatusAsync(Guid tenantId, Dictionary<string, PluginLoadStatus> status)
    {
        _store.TryAdd(tenantId.ToString("N"), new PluginLoadStatusDocument { LoadStatus = status });
    }

    public Task ClearPluginLoadStatusAsync()
    {
        return Task.CompletedTask;
    }

    public bool? IsChangeTrackingEnabled { get; }

    public Task<List<PluginLoadStatusDocument>> GetListAsync(bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<long> GetCountAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<List<PluginLoadStatusDocument>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting,
        bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public IQueryable<PluginLoadStatusDocument> WithDetails()
    {
        throw new NotImplementedException();
    }

    public IQueryable<PluginLoadStatusDocument> WithDetails(
        params Expression<Func<PluginLoadStatusDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<PluginLoadStatusDocument>> WithDetailsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<PluginLoadStatusDocument>> WithDetailsAsync(
        params Expression<Func<PluginLoadStatusDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<PluginLoadStatusDocument>> GetQueryableAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<PluginLoadStatusDocument>> GetListAsync(Expression<Func<PluginLoadStatusDocument, bool>> predicate,
        bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public IAsyncQueryableExecuter AsyncExecuter { get; }

    public Task<PluginLoadStatusDocument> InsertAsync(PluginLoadStatusDocument entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task InsertManyAsync(IEnumerable<PluginLoadStatusDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginLoadStatusDocument> UpdateAsync(PluginLoadStatusDocument entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task UpdateManyAsync(IEnumerable<PluginLoadStatusDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(PluginLoadStatusDocument entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteManyAsync(IEnumerable<PluginLoadStatusDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginLoadStatusDocument?> FindAsync(Expression<Func<PluginLoadStatusDocument, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginLoadStatusDocument> GetAsync(Expression<Func<PluginLoadStatusDocument, bool>> predicate,
        bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Expression<Func<PluginLoadStatusDocument, bool>> predicate, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteDirectAsync(Expression<Func<PluginLoadStatusDocument, bool>> predicate,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginLoadStatusDocument> GetAsync(string id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PluginLoadStatusDocument?> FindAsync(string id, bool includeDetails = true,
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
}