using System.Collections.Concurrent;
using System.Linq.Expressions;
using Aevatar.Core.Abstractions;
using Aevatar.Plugins.Entities;
using Aevatar.Plugins.GAgents;
using Aevatar.Plugins.Repositories;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace Aevatar.GAgents.Tests;

public class InMemoryTenantPluginCodeRepository : ITenantPluginCodeRepository
{
    private readonly ConcurrentDictionary<string, TenantPluginCodeSnapshotDocument> _store = new();

    public Task<IReadOnlyList<Guid>?> GetGAgentPrimaryKeysByTenantIdAsync(Guid tenantId)
    {
        var grainTypeName = typeof(TenantPluginCodeGAgent).FullName!;
        var grainIdString = $"{grainTypeName}/{tenantId:N}";
        if (_store.TryGetValue(grainIdString, out var doc))
        {
            return Task.FromResult((IReadOnlyList<Guid>?)doc.Doc.Snapshot.CodeStorageGuids?.Values);
        }

        return Task.FromResult<IReadOnlyList<Guid>?>(null);
    }


    public IQueryable<TenantPluginCodeSnapshotDocument> WithDetails()
    {
        throw new NotImplementedException();
    }

    public IQueryable<TenantPluginCodeSnapshotDocument> WithDetails(
        params Expression<Func<TenantPluginCodeSnapshotDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    Task<IQueryable<TenantPluginCodeSnapshotDocument>> IReadOnlyRepository<TenantPluginCodeSnapshotDocument>.
        WithDetailsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<TenantPluginCodeSnapshotDocument>> WithDetailsAsync(
        params Expression<Func<TenantPluginCodeSnapshotDocument, object>>[] propertySelectors)
    {
        throw new NotImplementedException();
    }

    public Task<IQueryable<TenantPluginCodeSnapshotDocument>> GetQueryableAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<TenantPluginCodeSnapshotDocument>> GetListAsync(
        Expression<Func<TenantPluginCodeSnapshotDocument, bool>> predicate, bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public IAsyncQueryableExecuter AsyncExecuter { get; }

    public Task<List<TenantPluginCodeSnapshotDocument>> WithDetailsAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public Task<List<TenantPluginCodeSnapshotDocument>> GetAllListAsync()
    {
        return Task.FromResult(_store.Values.ToList());
    }

    public bool? IsChangeTrackingEnabled { get; }

    public Task<List<TenantPluginCodeSnapshotDocument>> GetListAsync(bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<long> GetCountAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<List<TenantPluginCodeSnapshotDocument>> GetPagedListAsync(int skipCount, int maxResultCount,
        string sorting, bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument> InsertAsync(TenantPluginCodeSnapshotDocument entity,
        bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task InsertManyAsync(IEnumerable<TenantPluginCodeSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument> UpdateAsync(TenantPluginCodeSnapshotDocument entity,
        bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task UpdateManyAsync(IEnumerable<TenantPluginCodeSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(TenantPluginCodeSnapshotDocument entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteManyAsync(IEnumerable<TenantPluginCodeSnapshotDocument> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument?> FindAsync(
        Expression<Func<TenantPluginCodeSnapshotDocument, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument> GetAsync(
        Expression<Func<TenantPluginCodeSnapshotDocument, bool>> predicate, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Expression<Func<TenantPluginCodeSnapshotDocument, bool>> predicate, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteDirectAsync(Expression<Func<TenantPluginCodeSnapshotDocument, bool>> predicate,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument> GetAsync(string id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<TenantPluginCodeSnapshotDocument?> FindAsync(string id, bool includeDetails = true,
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

    public async Task SyncStoreAsync(IStateGAgent<TenantPluginCodeGAgentState> gAgent)
    {
        var tenantId = gAgent.GetPrimaryKey();
        var grainTypeName = typeof(TenantPluginCodeGAgent).FullName!;
        var id = $"{grainTypeName}/{tenantId:N}";
        var state = await gAgent.GetStateAsync();
        PerformSyncState(id, state);
    }

    private void PerformSyncState(string id, TenantPluginCodeGAgentState state)
    {
        var guidList = new CodeStorageGuidList { Values = state.CodeStorageGuids };
        if (_store.TryGetValue(id, out var doc))
        {
            doc.Doc.Snapshot.CodeStorageGuids = guidList;
        }
        else
        {
            _store[id] = new TenantPluginCodeSnapshotDocument
            {
                Doc = new TenantPluginCodeDocEntity
                {
                    Snapshot = new TenantPluginCodeSnapshotEntity
                    {
                        CodeStorageGuids = guidList
                    }
                }
            };
        }
    }
}