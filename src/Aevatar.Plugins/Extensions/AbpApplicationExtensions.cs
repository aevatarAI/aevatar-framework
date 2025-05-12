using System.Reflection;
using Aevatar.Core.Abstractions;
using Aevatar.Core.Abstractions.Extensions;
using Aevatar.Plugins.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Uow;
using Aevatar.Core.Abstractions.Plugin;

namespace Aevatar.Plugins.Extensions;

public static class AbpApplicationExtensions
{
    /// <summary>
    /// Loads and returns all plugin assemblies for the current tenant, with detailed load status tracking.
    /// </summary>
    public static async Task<List<Assembly>> GetTenantPluginAssemblyListAsync(
        this IAbpApplicationWithInternalServiceProvider application)
    {
        // --- Service & Config Resolution ---
        var loggerFactory = application.ServiceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        var logger = loggerFactory.CreateLogger("GetTenantPluginAssemblyList");
        SafeLog(logger, LogLevel.Information, "Trying to get tenant plugin assembly list");

        var assemblies = new List<Assembly>();
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("Orleans");
        if (string.IsNullOrEmpty(connectionString))
            return assemblies;

        var unitOfWorkManager = application.Services.GetRequiredService<IUnitOfWorkManager>();
        using var uow = unitOfWorkManager.Begin(requiresNew: true);
        var pluginOptions = application.Services.GetRequiredService<IOptions<PluginGAgentLoadOptions>>().Value;
        var tenantId = pluginOptions.TenantId;
        var tenantPluginCodeRepository = application.Services.GetRequiredService<ITenantPluginCodeRepository>();
        var pluginCodeStorageRepository = application.Services.GetRequiredService<IPluginCodeStorageRepository>();
        var pluginLoadStatusRepository = application.Services.GetRequiredService<IPluginLoadStatusRepository>();

        // --- Plugin Code Fetch ---
        var pluginCodeGAgentPrimaryKeys =
            await tenantPluginCodeRepository.GetGAgentPrimaryKeysByTenantIdAsync(tenantId);
        if (pluginCodeGAgentPrimaryKeys == null)
            return assemblies;
        var pluginCodes =
            await pluginCodeStorageRepository.GetPluginCodesByGAgentPrimaryKeys(pluginCodeGAgentPrimaryKeys);

        // --- Load Status Preparation ---
        await pluginLoadStatusRepository.ClearPluginLoadStatusAsync();
        var loadStatusDict = new Dictionary<string, PluginLoadStatus>();
        var alreadyLoaded = new List<string>();
        var agentType = typeof(IGAgent);
        var classNameToAssembly = new Dictionary<string, string>();
        var domainAssembliesBeforeLoading = AppDomain.CurrentDomain.GetAssemblies().ToList();

        // --- Plugin Assembly Loading Loop ---
        for (var i = 0; i < pluginCodes.Count; i++)
        {
            var code = pluginCodes[i];
            var pluginKey = pluginCodeGAgentPrimaryKeys[i];
            var dllName = "Unknown";
            var status = new PluginLoadStatus();
            try
            {
                var assembly = Assembly.Load(code);
                var types = assembly.GetTypesIgnoringLoadException();
                dllName = assembly!.FullName;
                var domainAssemblyNames = domainAssembliesBeforeLoading.Select(a => a.FullName);
                if (domainAssemblyNames.Contains(dllName))
                {
                    status.Status = LoadStatus.AlreadyLoaded;
                    status.Reason = $"Assembly '{dllName}' is already loaded.";
                    SafeLog(logger, LogLevel.Error, $"Failed to load plugin assembly: {dllName}, Reason: {status.Reason}");
                }
                else
                {
                    var gAgentTypes = types.Where(t =>
                        agentType.IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false });
                    var duplicateClasses = DetectDuplicateGAgents(gAgentTypes, classNameToAssembly, assembly);
                    if (duplicateClasses.Any())
                    {
                        status.Status = LoadStatus.GAgentDuplicated;
                        status.Reason = $"Duplicate IGAgent classes found: {string.Join(", ", duplicateClasses)}";
                        SafeLog(logger, LogLevel.Error, $"Failed to load plugin assembly: {dllName}, Reason: {status.Reason}");
                    }
                    else if (alreadyLoaded.Contains(dllName!))
                    {
                        status.Status = LoadStatus.AlreadyLoaded;
                        status.Reason = $"Assembly '{dllName}' is already loaded.";
                        SafeLog(logger, LogLevel.Error, $"Failed to load plugin assembly: {dllName}, Reason: {status.Reason}");
                    }
                    else
                    {
                        assemblies.Add(assembly);
                        status.Status = LoadStatus.Success;
                        status.Reason = null;
                        SafeLog(logger, LogLevel.Information, $"Loaded plugin assembly: {dllName}");
                    }
                }
            }
            catch (Exception ex)
            {
                status.Status = LoadStatus.Error;
                status.Reason = ex.Message;
                SafeLog(logger, LogLevel.Error, $"Failed to load plugin assembly: {dllName}, Reason: {ex.Message}");
            }
            loadStatusDict[$"{dllName}_{pluginKey:N}"] = status;
            alreadyLoaded.Add(dllName!);
        }

        // --- Persist Load Status ---
        await pluginLoadStatusRepository.SetPluginLoadStatusAsync(tenantId, loadStatusDict);
        await uow.CompleteAsync();
        SafeLog(logger, LogLevel.Information, $"Tenant plugin assemblies count: {assemblies.Count}");
        return assemblies;
    }

    /// <summary>
    /// Detects duplicate IGAgent class names across loaded plugin assemblies.
    /// </summary>
    private static List<string> DetectDuplicateGAgents(IEnumerable<Type> types, Dictionary<string, string> classNameToAssembly, Assembly assembly)
    {
        var duplicateClasses = new List<string>();
        foreach (var type in types)
        {
            var className = type.Name;
            if (classNameToAssembly.TryGetValue(className, out var assemblyName))
            {
                duplicateClasses.Add($"{className} (defined in {assemblyName})");
            }
            else
            {
                classNameToAssembly[className] = assembly.FullName ?? assembly.GetName().Name ?? "UnknownAssembly";
            }
        }
        return duplicateClasses;
    }

    /// <summary>
    /// Logs safely to the provided logger, or to console if logger is not enabled.
    /// </summary>
    private static void SafeLog(ILogger logger, LogLevel logLevel, string content)
    {
        if (logger.IsEnabled(logLevel))
        {
            logger.Log(logLevel, content);
        }
        else
        {
            Console.WriteLine(content);
        }
    }
}