using Volo.Abp.PermissionManagement;

namespace Aevatar.GAgents.Tests;

public class MockPermissionGrantRepository : IPermissionGrantRepository
{
    private readonly List<PermissionGrant> _grants = new List<PermissionGrant>();


    public Task<PermissionGrant?> FindAsync(string name, string providerName, string providerKey,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.FromResult(_grants.FirstOrDefault(g =>
            g.Name == name &&
            g.ProviderName == providerName &&
            g.ProviderKey == providerKey
        ));
    }

    public Task<List<PermissionGrant>> GetListAsync(string providerName, string providerKey,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<List<PermissionGrant>> GetListAsync(string[] names, string providerName, string providerKey,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public bool? IsChangeTrackingEnabled { get; }

    public Task<List<PermissionGrant>> GetListAsync(bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<long> GetCountAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<List<PermissionGrant>> GetPagedListAsync(int skipCount, int maxResultCount, string sorting,
        bool includeDetails = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PermissionGrant> InsertAsync(PermissionGrant entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        _grants.Add(entity);
        return Task.FromResult(entity);
    }

    public Task InsertManyAsync(IEnumerable<PermissionGrant> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PermissionGrant> UpdateAsync(PermissionGrant entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task UpdateManyAsync(IEnumerable<PermissionGrant> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(PermissionGrant entity, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        _grants.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(IEnumerable<PermissionGrant> entities, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PermissionGrant> GetAsync(Guid id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task<PermissionGrant?> FindAsync(Guid id, bool includeDetails = true,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(Guid id, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public Task DeleteManyAsync(IEnumerable<Guid> ids, bool autoSave = false,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }
}