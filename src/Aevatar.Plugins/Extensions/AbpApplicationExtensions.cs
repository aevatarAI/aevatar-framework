using System.Reflection;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Uow;

namespace Aevatar.Plugins.Extensions;

public static class AbpApplicationExtensions
{
    public static async Task<List<Assembly>> GetTenantPluginAssemblyListAsync(
        this IAbpApplicationWithInternalServiceProvider application)
    {
        var assemblies = new List<Assembly>();
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrEmpty(connectionString))
        {
            return assemblies;
        }

        var unitOfWorkManager = application.Services.GetRequiredService<IUnitOfWorkManager>();
        using var uow = unitOfWorkManager.Begin(requiresNew: true);
        var pluginOptions = application.Services.GetRequiredService<IOptions<PluginGAgentLoadOptions>>().Value;
        var tenantId = pluginOptions.TenantId;
        var tenantPluginCodeRepository = application.Services.GetRequiredService<ITenantPluginCodeRepository>();
        var pluginCodeGAgentPrimaryKeys =
            await tenantPluginCodeRepository.GetGAgentPrimaryKeysByTenantIdAsync(tenantId);
        if (pluginCodeGAgentPrimaryKeys == null) return assemblies;
        var pluginCodeStorageRepository = application.Services.GetRequiredService<IPluginCodeStorageRepository>();
        var pluginCodes =
            await pluginCodeStorageRepository.GetPluginCodesByGAgentPrimaryKeys(pluginCodeGAgentPrimaryKeys);
        assemblies = pluginCodes.Select(Assembly.Load).DistinctBy(assembly => assembly.FullName).ToList();
        await uow.CompleteAsync();
        return assemblies;
    }
}