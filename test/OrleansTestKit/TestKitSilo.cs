using System.Linq.Expressions;
using Aevatar.Core.Abstractions;
using Aevatar.EventSourcing.Core;
using Aevatar.EventSourcing.Core.Hosting;
using Aevatar.EventSourcing.Core.LogConsistency;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Orleans.EventSourcing;
using Orleans.Metadata;
using Orleans.Serialization;
using Orleans.Serialization.Cloning;
using Orleans.Serialization.Configuration;
using Orleans.Serialization.Serializers;
using Orleans.Storage;
using Orleans.TestKit.Reminders;
using Orleans.TestKit.Services;
using Orleans.TestKit.Storage;
using Orleans.TestKit.Streams;
using Orleans.TestKit.Timers;
using Orleans.Timers;

namespace Orleans.TestKit;

/// <summary>
/// The central abstraction for the OrleansTestKit -- stands up multiple services and provides a centralized abstraction for interacting with test grains
/// </summary>
public sealed class TestKitSilo
{
    private readonly List<IGrainBase> _activatedGrains = new();

    private readonly TestGrainCreator _grainCreator;

    private readonly TestGrainLifecycle _grainLifecycle = new();

    private readonly GrainTypeResolver _grainTypeResolver;

    private readonly TestGrainRuntime _grainRuntime;

    private readonly Dictionary<GrainId, IGrainBase> _createdGrains = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="TestKitSilo"/> class.
    /// </summary>
    public TestKitSilo()
    {
        GrainFactory = new TestGrainFactory(Options);
        ServiceProvider = new TestServiceProvider(Options);
        StorageManager = new StorageManager(Options);
        TestGrainStorage = new TestGrainStorage(StorageManager);
        TimerRegistry = new TestTimerRegistry();
        ReminderRegistry = new TestReminderRegistry();
        StreamProviderManager = new TestStreamProviderManager(ServiceProvider, Options);
        ServiceProvider.AddService<IReminderRegistry>(ReminderRegistry);

        // Event Sourcing
        var mockOptionsManager = new Mock<IOptions<TypeManifestOptions>>();
        mockOptionsManager.Setup(m => m.Value).Returns(new TypeManifestOptions());
        var codecProvider = new CodecProvider(ServiceProvider, mockOptionsManager.Object);
        LogConsistencyProvider =
            new TestLogConsistencyProvider(ServiceProvider, new InMemoryLogConsistentStorage(), TestGrainStorage);
        ServiceProvider.AddKeyedService<ILogViewAdaptorFactory>("LogStorage", LogConsistencyProvider);
        ProtocolServices = new DefaultProtocolServices(new Mock<IGrainContext>().Object, NullLoggerFactory.Instance,
            new DeepCopier(codecProvider, new CopyContextPool(codecProvider)), null!);
        ServiceProvider.AddService<ILogConsistencyProtocolServices>(ProtocolServices);
        ServiceProvider.AddService<Factory<IGrainContext, ILogConsistencyProtocolServices>>(sp =>
            ProtocolServices);

        GrainRuntime =
            new TestGrainRuntime(GrainFactory, TimerRegistry, ReminderRegistry, ServiceProvider, StorageManager);
        ServiceProvider.AddService<IGrainRuntime>(GrainRuntime);
        _grainCreator = new TestGrainCreator(GrainRuntime, ReminderRegistry, TestGrainStorage, ServiceProvider);

        ServiceProvider.AddService<IGrainStorage>(TestGrainStorage);
        var provider = new ServiceCollection()
            .AddSingleton<GrainTypeResolver>()
            .AddSingleton<IGrainTypeProvider, AttributeGrainTypeProvider>()
            .AddSerializer()
            .AddInMemoryBasedLogConsistencyProvider("LogStorage")
            .BuildServiceProvider();

        _grainTypeResolver = provider.GetRequiredService<GrainTypeResolver>();
    }

    /// <summary>
    /// Gets the silo's grain runtime instance that is injected into the created grain
    /// </summary>
    public TestGrainRuntime GrainRuntime { get; }

    /// <summary>
    ///     Gets the silo grain factory used by the test grain with creating other grains This should only be used by
    ///     the grain, not any test code.
    /// </summary>
    public TestGrainFactory GrainFactory { get; }

    /// <summary>Gets the test silo options.</summary>
    public TestKitOptions Options { get; } = new();

    /// <summary>Gets the manager of all test silo reminders.</summary>
    public TestReminderRegistry ReminderRegistry { get; }

    /// <summary>Gets the service provider used when creating new instances.</summary>
    public TestServiceProvider ServiceProvider { get; }

    /// <summary>Gets the manager of all test silo storage.</summary>
    public StorageManager StorageManager { get; }

    public TestGrainStorage TestGrainStorage { get; set; }

    /// <summary>Gets the manager of all test silo streams.</summary>
    public TestStreamProviderManager StreamProviderManager { get; }

    /// <summary>Gets the manager of all test silo timers.</summary>
    public TestTimerRegistry TimerRegistry { get; }

    public TestLogConsistencyProvider LogConsistencyProvider { get; set; }
    public DefaultProtocolServices ProtocolServices { get; set; }
    public InMemoryLogConsistentStorage TestLogConsistentStorage { get; set; } = new();

    public Task<T> CreateGrainAsync<T>(long id) where T : IGrainBase, IGrainWithIntegerKey =>
        CreateGrainAsync<T>(GrainIdKeyExtensions.CreateIntegerKey(id));

    public Task<T> CreateGrainAsync<T>(Guid id) where T : IGrainBase, IGrainWithGuidKey =>
        CreateGrainAsync<T>(GrainIdKeyExtensions.CreateGuidKey(id));

    public Task<T> CreateGrainAsync<T>(string id) where T : IGrainBase, IGrainWithStringKey =>
        CreateGrainAsync<T>(IdSpan.Create(id));

    public Task<T> CreateGrainAsync<T>(Guid id, string keyExtension) where T : IGrainBase, IGrainWithGuidCompoundKey =>
        CreateGrainAsync<T>(GrainIdKeyExtensions.CreateGuidKey(id, keyExtension));

    public Task<T> CreateGrainAsync<T>(long id, string keyExtension)
        where T : IGrainBase, IGrainWithIntegerCompoundKey =>
        CreateGrainAsync<T>(GrainIdKeyExtensions.CreateIntegerKey(id, keyExtension));

    /// <summary>Deactivate the given <see cref="Grain"/>.</summary>
    /// <param name="grain">Grain to Deactivate.</param>
    /// <param name="deactivationReason">
    ///     Reason which will passed to the Grian <seealso cref="DeactivationReason"/>.
    /// </param>
    /// <param name="cancellationToken">Token which will passed to the grain.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeactivateAsync(Grain grain, DeactivationReason? deactivationReason = null,
        CancellationToken cancellationToken = default)
    {
        if (grain == null)
        {
            throw new ArgumentNullException(nameof(grain));
        }

        await _grainLifecycle.TriggerStopAsync().ConfigureAwait(false);

        deactivationReason ??= new DeactivationReason(DeactivationReasonCode.ShuttingDown,
            $"TestKit {nameof(DeactivateAsync)} called");
        await grain.OnDeactivateAsync(deactivationReason.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches Grain Context.</summary>
    /// <param name="grain">Grain to fetch Context from.</param>
    /// <returns><see cref="IGrainContext"/>.</returns>
    /// <exception cref="NotSupportedException">Grain does not derive from <see cref="IGrainBase"/>.</exception>
    public IGrainContext GetContextFromGrain(IGrainBase grain) => grain.GrainContext;

    /// <summary>Fetches <see cref="GrainId"/> from current Grain.</summary>
    /// <param name="grain">Grain to fetch Grain Id.</param>
    /// <returns><see cref="GrainId"/>.</returns>
    public GrainId GetGrainId(Grain grain) => GrainId.Parse(grain.IdentityString);

    /// <summary>
    /// Used for interacting with test reminders -- this will be removed in a later version as Orleans has alleviated the need for this.
    /// </summary>
    /// <param name="grain">The grain to create the context for</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>A disposable to denote when the context is finished.</returns>
    public async Task<IDisposable> GetReminderActivationContext(IGrainBase grain, CancellationToken token = default)
    {
        // TODO -- v5, mark this as obsolete when we support Orleans 8 only.
        var handler = new ReminderContextHandler();

        try
        {
            await handler.SetActivationContext(GetContextFromGrain(grain), token).ConfigureAwait(false);
        }
        catch
        {
            // Release Thread to avoid Deadlocks when an Exception happens.
            handler.Dispose();
            throw;
        }

        return handler;
    }

    /// <summary>
    /// Verify the grain runtime object's mock
    /// </summary>
    /// <param name="expression">The expression to use</param>
    /// <param name="times">Number of times expected</param>
    [Obsolete("Use the Runtime property instead. This will be removed in a future version.")]
    public void VerifyRuntime(Expression<Action<IGrainRuntime>> expression, Func<Times> times) =>
        GrainRuntime.Mock.Verify(expression, times);

    /// <summary>
    /// Gets or adds a grain context for the current ID - useful if needing to create a context earlier in the lifecycle (pre-grain creation)
    /// </summary>
    /// <typeparam name="T">The grain type</typeparam>
    /// <param name="identity"></param>
    /// <returns></returns>
    public IGrainContext GetOrAddGrainContext<T>(IdSpan identity)
        where T : IGrainBase
    {
        var grainType = _grainTypeResolver.GetGrainType(typeof(T));
        var grainId = GrainId.Create(grainType, identity);

        if (ServiceProvider.GetService<IGrainContext>() is not TestGrainActivationContext context || context.GrainId != grainId || context.GrainType != typeof(T))
        {
            // we have not registered a context yet OR we have registered a context but it is for a different grain and we need to re-create
            context = new TestGrainActivationContext
            {
                GrainId = grainId,
                ActivationServices = ServiceProvider,
                GrainType = typeof(T),
                ObservableLifecycle = _grainLifecycle,
                GrainInstance = new Mock<IGrainBase>().Object
            };

            // make context injectable so grain dependency components can inject IGrainContext directly themselves
            ServiceProvider.AddService<IGrainContext>(context);
            ServiceProvider.AddService<IGrainContextAccessor>(new TestGrainContextAccessor(context));
        }

        return context;
    }

    /// <summary>
    /// The core grain creation method -- all strongly typed CreateGrainAsync extensions route to this
    /// </summary>
    /// <typeparam name="T">The grain type</typeparam>
    /// <param name="identity">The grain identity</param>
    /// <param name="cancellation">Cancellation token</param>
    /// <returns>The grain</returns>
    public async Task<T> CreateGrainAsync<T>(IdSpan identity, CancellationToken cancellation = default)
        where T : IGrainBase
    {
        var grainType = _grainTypeResolver.GetGrainType(typeof(T));
        var grainId = GrainId.Create(grainType, identity);
        if (_createdGrains.TryGetValue(grainId, out var storedGrain))
        {
            var createdGrain = (T)storedGrain;
            if (typeof(IGAgent).IsAssignableFrom(typeof(T)) && ((IGAgent)createdGrain).GetGrainId().Key == identity)
            {
                return createdGrain;
            }
        }

        // Add state attribute mapping for storage facets
        _ = this.AddService<IAttributeToFactoryMapper<PersistentStateAttribute>>(StorageManager
            .StateAttributeFactoryMapper);

        var grainContext = GetOrAddGrainContext<T>(identity);

        // Create a stateless grain
        var grain = _grainCreator.CreateGrainInstance<T>(grainContext);
        switch (grain)
        {
            case null:
                throw new Exception($"Unable to instantiate grain {typeof(T)} properly");

            // Check if there are any reminders for this grain and set the reminder target
            case IRemindable remindable:
                ReminderRegistry.SetGrainTarget(remindable);
                break;
        }

        // Trigger the lifecycle hook that will get the grain's state from the runtime
        await _grainLifecycle.TriggerStartAsync().ConfigureAwait(false);

        // Due to update the OnActivate call has to be done manually not all Grains implement ILifecycle anymore

        if (grain is IRemindable)
        {
            // Used to enable reminder context on during activate
            using var reminderContext =
                await GetReminderActivationContext(grain, cancellation).ConfigureAwait(false);
        }

        await grain.OnActivateAsync(cancellation).ConfigureAwait(false);
        _activatedGrains.Add(grain);

        _createdGrains[grainId] = grain;

        return (T)grain;
    }
}
