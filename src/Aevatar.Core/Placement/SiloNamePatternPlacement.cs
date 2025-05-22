using Orleans.Metadata;
using Orleans.Placement;
using Orleans.Runtime.Placement;
using Microsoft.Extensions.Logging;

namespace Aevatar.Core.Placement
{
    /// <summary>
    /// A placement strategy which places grains on silos based on a name pattern.
    /// This allows for targeting silos whose names contain a specific substring.
    /// </summary>
    [Serializable, GenerateSerializer, Immutable, SuppressReferenceTracking]
    public sealed class SiloNamePatternPlacement : PlacementStrategy
    {
        /// <summary>
        /// Gets the singleton instance of this class.
        /// </summary>
        internal static SiloNamePatternPlacement Singleton { get; } = new SiloNamePatternPlacement();

        /// <summary>
        /// The property key used to store the silo name pattern in grain properties.
        /// </summary>
        public const string SiloNamePatternPropertyKey = "Aevatar.Placement.SiloNamePattern";

        /// <summary>
        /// Gets or sets the silo name pattern used to match against silo names.
        /// Grain will be activated on a silo whose name begins with this pattern.
        /// </summary>
        [Id(0)]
        public string SiloNamePattern { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SiloNamePatternPlacement"/> class.
        /// </summary>
        public SiloNamePatternPlacement()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SiloNamePatternPlacement"/> class with the specified silo name pattern.
        /// </summary>
        /// <param name="siloNamePattern">The pattern to match against silo names. Grain will be activated on a silo whose name begins with this pattern.</param>
        /// <returns>A new instance of <see cref="SiloNamePatternPlacement"/> with the specified silo name pattern.</returns>
        public static SiloNamePatternPlacement Create(string siloNamePattern)
        {
            var instance = new SiloNamePatternPlacement
            {
                SiloNamePattern = siloNamePattern
            };
            return instance;
        }

        /// <summary>
        /// Initializes an instance of this type using the provided grain properties.
        /// </summary>
        /// <param name="properties">The grain properties.</param>
        public override void Initialize(GrainProperties properties)
        {
            base.Initialize(properties);
            
            // Extract silo name pattern from grain properties if available
            if (properties.Properties.TryGetValue(SiloNamePatternPropertyKey, out var siloNamePattern))
            {
                SiloNamePattern = siloNamePattern;
            }
        }

        /// <summary>
        /// Populates grain properties to specify the preferred placement strategy.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="grainClass">The grain class.</param>
        /// <param name="grainType">The grain type.</param>
        /// <param name="properties">The grain properties which will be populated by this method call.</param>
        public override void PopulateGrainProperties(IServiceProvider services, Type grainClass, GrainType grainType, Dictionary<string, string> properties)
        {
            base.PopulateGrainProperties(services, grainClass, grainType, properties);
            
            // Store the silo name pattern in grain properties
            if (!string.IsNullOrWhiteSpace(SiloNamePattern))
            {
                properties[SiloNamePatternPropertyKey] = SiloNamePattern;
            }
        }
    }

    /// <summary>
    /// A placement director which places grain activations on silos whose names match a specified pattern.
    /// </summary>
    public class SiloNamePatternPlacementDirector : IPlacementDirector
    {
        private readonly ISiloStatusOracle _siloStatusOracle;
        private readonly GrainPropertiesResolver _grainPropertiesResolver;
        private readonly ILogger<SiloNamePatternPlacementDirector> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SiloNamePatternPlacementDirector"/> class.
        /// </summary>
        /// <param name="siloStatusOracle">The silo status oracle.</param>
        public SiloNamePatternPlacementDirector(
            ISiloStatusOracle siloStatusOracle,
            GrainPropertiesResolver grainPropertiesResolver,
            ILogger<SiloNamePatternPlacementDirector> logger)
        {
            _siloStatusOracle = siloStatusOracle ?? throw new ArgumentNullException(nameof(siloStatusOracle));
            _grainPropertiesResolver = grainPropertiesResolver ?? throw new ArgumentNullException(nameof(grainPropertiesResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Picks an appropriate silo to place the specified target on based on the silo name pattern.
        /// </summary>
        /// <param name="strategy">The target's placement strategy.</param>
        /// <param name="target">The grain being placed as well as information about the request which triggered the placement.</param>
        /// <param name="context">The placement context.</param>
        /// <returns>An appropriate silo to place the specified target on.</returns>
        public Task<SiloAddress> OnAddActivation(PlacementStrategy strategy, PlacementTarget target, IPlacementContext context)
        {
            // Get pattern from grain properties
            string siloNamePattern = null;
            
            // Try to get the grain properties for this grain type
            if (_grainPropertiesResolver.TryGetGrainProperties(target.GrainIdentity.Type, out var properties) && 
                properties.Properties.TryGetValue(SiloNamePatternPlacement.SiloNamePatternPropertyKey, out var pattern))
            {
                siloNamePattern = pattern;
            }

            if (string.IsNullOrWhiteSpace(siloNamePattern))
            {
                throw new OrleansException($"SiloNamePatternPlacement strategy requires a valid silo name pattern. " +
                                            $"Current pattern: '{siloNamePattern}'");
            }

            var compatibleSilos = context.GetCompatibleSilos(target);

            // If a valid placement hint was specified, use it.
            if (IPlacementDirector.GetPlacementHint(target.RequestContextData, compatibleSilos) is { } placementHint)
            {
                return Task.FromResult(placementHint);
            }

            // Get all active silos
            // Find all active silos whose names contain the specified pattern
            var matchingSilos = new List<SiloAddress>();
            // Iterate over compatible silos directly (likely fewer items)
            foreach (var siloAddress in compatibleSilos)
            {
                // Only check status/name for compatible silos
                if (_siloStatusOracle.TryGetSiloName(siloAddress, out var siloName) &&
                    siloName.StartsWith(siloNamePattern, StringComparison.OrdinalIgnoreCase))
                {
                    matchingSilos.Add(siloAddress);
                }
            }

            if (matchingSilos.Count == 0)
            {
                // If no matching silos found, throw an exception
                throw new OrleansException($"No silos matching pattern '{siloNamePattern}' found. Available silos: {string.Join(", ", compatibleSilos.Select(s => s.ToString()))}");
            }

            // Randomly select one of the matching silos
            var idx = Random.Shared.Next(matchingSilos.Count);
            _logger.LogDebug("[SiloNamePatternPlacement] GrainId={GrainId}, Pattern={Pattern}, compatibleSiloCount={CompatibleSiloCount}, matchingSiloCount={MatchingCount}, idx ={Idx}",
                target.GrainIdentity, siloNamePattern, compatibleSilos.Length, matchingSilos.Count,idx);
            return Task.FromResult(matchingSilos[idx]);
        }
    }

    /// <summary>
    /// Attribute used to specify that a grain should be placed on silos whose names match a specified pattern.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SiloNamePatternPlacementAttribute : PlacementAttribute
    {
        /// <summary>
        /// Gets the name pattern to match silos. Grain will be activated on a silo whose name begins with this pattern.
        /// </summary>
        public string SiloNamePattern { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SiloNamePatternPlacementAttribute"/> class.
        /// </summary>
        /// <param name="siloNamePattern">The pattern to match silo names. Grain will be activated on a silo whose name begins with this pattern.</param>
        public SiloNamePatternPlacementAttribute(string siloNamePattern)
            : base(SiloNamePatternPlacement.Create(siloNamePattern))
        {
            SiloNamePattern = siloNamePattern ?? throw new ArgumentNullException(nameof(siloNamePattern));
        }
    }
} 