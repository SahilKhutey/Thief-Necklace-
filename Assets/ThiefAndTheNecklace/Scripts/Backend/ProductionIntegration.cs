using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace.Networking;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Backend
{
    public enum ProductionBuildTarget
    {
        WindowsDevelopment, WindowsQa, AndroidDevelopment, AndroidQa, AndroidRelease,
        IosQa, IosRelease, WebGlDevelopment, WebGlQa, WebGlRelease
    }

    public enum BuildProfileKind { Development, Qa, Release }

    public sealed class BuildProfileSettings
    {
        public BuildProfileKind Kind { get; }
        public bool DebugOverlayEnabled { get; }
        public bool MutationToolsEnabled { get; }
        public bool FaultInjectionEnabled { get; }
        public bool SyntheticAccountsEnabled { get; }
        public bool TelemetryEnabled { get; }
        public static BuildProfileSettings For(BuildProfileKind kind)
        {
            switch (kind)
            {
                case BuildProfileKind.Development: return new BuildProfileSettings(kind, true, true, true, true, false);
                case BuildProfileKind.Qa: return new BuildProfileSettings(kind, true, false, false, false, true);
                case BuildProfileKind.Release: return new BuildProfileSettings(kind, false, false, false, false, true);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        private BuildProfileSettings(BuildProfileKind kind, bool debug, bool mutation, bool faults, bool synthetic, bool telemetry)
        { Kind=kind; DebugOverlayEnabled=debug; MutationToolsEnabled=mutation; FaultInjectionEnabled=faults; SyntheticAccountsEnabled=synthetic; TelemetryEnabled=telemetry; }
    }

    /// <summary>Non-secret build identity passed to diagnostics and support tooling.</summary>
    public sealed class BuildMetadata
    {
        public string ApplicationId { get; }
        public string GameVersion { get; }
        public int BuildNumber { get; }
        public int NetworkProtocolVersion { get; }
        public int RulesVersion { get; }
        public int ScenarioVersion { get; }
        public string SourceRevision { get; }
        public string Target { get; }

        public BuildMetadata(string applicationId, string gameVersion, int buildNumber, int networkProtocolVersion,
            int rulesVersion, int scenarioVersion, string sourceRevision, string target)
        {
            ApplicationId=applicationId ?? string.Empty; GameVersion=gameVersion ?? string.Empty;
            BuildNumber=buildNumber; NetworkProtocolVersion=networkProtocolVersion; RulesVersion=rulesVersion;
            ScenarioVersion=scenarioVersion; SourceRevision=sourceRevision ?? string.Empty; Target=target ?? string.Empty;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors=new List<string>();
            if (string.IsNullOrWhiteSpace(ApplicationId)) errors.Add("Build application ID is missing.");
            if (string.IsNullOrWhiteSpace(GameVersion)) errors.Add("Build game version is missing.");
            if (BuildNumber <= 0) errors.Add("Build number must be positive.");
            if (NetworkProtocolVersion <= 0 || RulesVersion <= 0 || ScenarioVersion <= 0) errors.Add("Protocol, rules, and scenario versions must be positive.");
            if (string.IsNullOrWhiteSpace(SourceRevision)) errors.Add("Source revision is missing.");
            if (string.IsNullOrWhiteSpace(Target)) errors.Add("Build target is missing.");
            return errors.AsReadOnly();
        }
    }

    public sealed class RuntimeServiceHealth
    {
        public string ServiceId { get; }
        public BackendHealthState State { get; }
        public string FailureCode { get; }
        public RuntimeServiceHealth(string serviceId, BackendHealthState state, string failureCode = null)
        { ServiceId=serviceId ?? string.Empty; State=state; FailureCode=failureCode ?? string.Empty; }
    }

    public sealed class RuntimeHealthReport
    {
        public IReadOnlyList<RuntimeServiceHealth> Services { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsHealthy => Errors.Count == 0;
        internal RuntimeHealthReport(IEnumerable<RuntimeServiceHealth> services, IEnumerable<string> errors)
        { Services=Array.AsReadOnly(services.ToArray()); Errors=Array.AsReadOnly(errors.ToArray()); }
    }

    public static class RuntimeHealthGate
    {
        public static RuntimeHealthReport Check(IEnumerable<RuntimeServiceHealth> services, IEnumerable<string> requiredServiceIds)
        {
            var items=(services ?? Enumerable.Empty<RuntimeServiceHealth>()).Where(item => item != null).ToArray();
            var errors=new List<string>();
            var index=items.GroupBy(item => item.ServiceId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
            foreach (var id in (requiredServiceIds ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
            {
                RuntimeServiceHealth health;
                if (!index.TryGetValue(id, out health)) errors.Add("Required runtime service is missing: " + id + ".");
                else if (health.State != BackendHealthState.Healthy) errors.Add("Required runtime service is not healthy: " + id + " (" + health.State + ").");
            }
            return new RuntimeHealthReport(items, errors);
        }
    }

    public sealed class MatchReadinessInput
    {
        public bool NetworkConnected { get; set; }
        public bool RoomValid { get; set; }
        public bool ConfigurationValid { get; set; }
        public bool AuthorityAvailable { get; set; }
        public int PlayerCount { get; set; }
        public RuntimeHealthReport ServiceHealth { get; set; }
    }

    public static class MatchReadinessGate
    {
        public static IReadOnlyList<string> Validate(MatchReadinessInput input)
        {
            var errors=new List<string>();
            if (input == null) return new[] { "Match readiness input is missing." };
            if (!input.NetworkConnected) errors.Add("Network is not connected.");
            if (!input.RoomValid) errors.Add("Room or roster is not valid.");
            if (!input.ConfigurationValid) errors.Add("Match configuration is invalid.");
            if (!input.AuthorityAvailable) errors.Add("Gameplay authority is unavailable.");
            if (input.PlayerCount < 4 || input.PlayerCount > 6) errors.Add("Match requires 4–6 players under the implemented rules.");
            if (input.ServiceHealth == null || !input.ServiceHealth.IsHealthy)
                errors.AddRange(input.ServiceHealth == null ? new[] { "Runtime service health has not passed." } : input.ServiceHealth.Errors);
            return errors.AsReadOnly();
        }
    }

    /// <summary>Non-secret, per-release inputs for the production integration gate.</summary>
    public sealed class ProductionIntegrationManifest
    {
        public EnvironmentConfig Environment { get; }
        public ClientVersionInfo Version { get; }
        public BuildMetadata Build { get; }
        public FrozenProductionConfiguration FrozenConfiguration { get; }
        public string ApplicationId { get; }
        public int MinimumPlayers { get; }
        public int MaximumPlayers { get; }
        public bool DebugMutationToolsEnabled { get; }
        public bool FaultInjectionEnabled { get; }
        public bool DeveloperInspectorEnabled { get; }
        public IReadOnlyList<ProductionBuildTarget> RequiredBuildTargets { get; }

        public ProductionIntegrationManifest(EnvironmentConfig environment, ClientVersionInfo version,
            string applicationId, int minimumPlayers, int maximumPlayers,
            bool debugMutationToolsEnabled, bool faultInjectionEnabled, bool developerInspectorEnabled,
            IEnumerable<ProductionBuildTarget> requiredBuildTargets, BuildMetadata buildMetadata = null,
            FrozenProductionConfiguration frozenConfiguration = null)
        {
            Environment=environment; Version=version; Build=buildMetadata; FrozenConfiguration=frozenConfiguration; ApplicationId=applicationId ?? string.Empty;
            MinimumPlayers=minimumPlayers; MaximumPlayers=maximumPlayers;
            DebugMutationToolsEnabled=debugMutationToolsEnabled; FaultInjectionEnabled=faultInjectionEnabled;
            DeveloperInspectorEnabled=developerInspectorEnabled;
            RequiredBuildTargets=Array.AsReadOnly((requiredBuildTargets ?? Enumerable.Empty<ProductionBuildTarget>()).Distinct().ToArray());
        }
    }

    /// <summary>Concrete adapter bindings supplied by the host application; never stores credentials.</summary>
    public sealed class ProductionServiceBindings
    {
        public IAuthenticatedPlayer Authentication { get; }
        public INetworkBackend Network { get; }
        public IMatchmakingService Matchmaking { get; }
        public IProductionProfileApi Profiles { get; }
        public IBackendHealthProbe Health { get; }

        public ProductionServiceBindings(IAuthenticatedPlayer authentication, INetworkBackend network,
            IMatchmakingService matchmaking, IProductionProfileApi profiles, IBackendHealthProbe health)
        {
            Authentication=authentication; Network=network; Matchmaking=matchmaking; Profiles=profiles; Health=health;
        }
    }

    public sealed class ProductionIntegrationReport
    {
        public bool IsReady => Errors.Count == 0;
        public IReadOnlyList<string> Errors { get; }
        internal ProductionIntegrationReport(IEnumerable<string> errors) => Errors=Array.AsReadOnly(errors.ToArray());
    }

    /// <summary>Validates the actual runtime composition and packaging inputs before a production build.</summary>
    public static class ProductionIntegrationGate
    {
        private static readonly ProductionBuildTarget[] RequiredTargets =
        {
            ProductionBuildTarget.WindowsDevelopment, ProductionBuildTarget.WindowsQa,
            ProductionBuildTarget.AndroidDevelopment, ProductionBuildTarget.AndroidQa, ProductionBuildTarget.AndroidRelease,
            ProductionBuildTarget.IosQa, ProductionBuildTarget.IosRelease,
            ProductionBuildTarget.WebGlDevelopment, ProductionBuildTarget.WebGlQa, ProductionBuildTarget.WebGlRelease
        };

        public static ProductionIntegrationReport Validate(ProductionIntegrationManifest manifest, ProductionServiceBindings bindings)
        {
            var errors=new List<string>();
            if (manifest == null) return new ProductionIntegrationReport(new[] { "Production integration manifest is missing." });
            if (manifest.Environment == null || manifest.Environment.Environment != EnvironmentType.Production || !manifest.Environment.IsValid)
                errors.Add("Production environment configuration is missing or not set to Production.");
            else if (manifest.Environment.EnableDebugTools || manifest.Environment.EnableSyntheticAccounts)
                errors.Add("Debug tools and synthetic accounts must be disabled in Production.");
            if (manifest.Version == null || string.IsNullOrWhiteSpace(manifest.Version.GameVersion) || manifest.Version.ContentVersion <= 0 || manifest.Version.ProfileSchemaVersion <= 0 || manifest.Version.NetworkProtocolVersion <= 0)
                errors.Add("Game, content, profile-schema, and network-protocol versions must be set to valid values.");
            if (manifest.Build == null) errors.Add("Unique build identity and source revision metadata are missing.");
            else
            {
                errors.AddRange(manifest.Build.Validate());
                if (manifest.Build.ApplicationId != manifest.ApplicationId) errors.Add("Build application identity does not match the production manifest.");
                if (manifest.Version != null && (manifest.Build.GameVersion != manifest.Version.GameVersion || manifest.Build.NetworkProtocolVersion != manifest.Version.NetworkProtocolVersion))
                    errors.Add("Build metadata does not match the client game/protocol version.");
            }
            if (manifest.FrozenConfiguration == null || manifest.FrozenConfiguration.Channel != ReleaseChannel.Production)
                errors.Add("A frozen Production configuration snapshot is missing.");
            if (IsPlaceholder(manifest.ApplicationId) || !IsSafeId(manifest.ApplicationId)) errors.Add("A real, bounded service application ID is required; placeholders are not release configuration.");
            if (manifest.MinimumPlayers != 4 || manifest.MaximumPlayers < manifest.MinimumPlayers || manifest.MaximumPlayers > 6)
                errors.Add("Configured room size must match the implemented gameplay range of 4–6 players.");
            if (manifest.DebugMutationToolsEnabled || manifest.FaultInjectionEnabled || manifest.DeveloperInspectorEnabled)
                errors.Add("Debug mutation, fault injection, and developer inspector must be disabled for Production.");

            var targets=new HashSet<ProductionBuildTarget>(manifest.RequiredBuildTargets ?? Array.Empty<ProductionBuildTarget>());
            if (targets.Any(target => !Enum.IsDefined(typeof(ProductionBuildTarget), target))) errors.Add("An unknown build target is configured.");
            foreach (var target in RequiredTargets)
                if (!targets.Contains(target)) errors.Add("Required build target is not configured: " + target + ".");

            if (bindings == null) errors.Add("Production service bindings are missing.");
            else
            {
                if (bindings.Authentication == null) errors.Add("Authenticated-player adapter is missing.");
                if (bindings.Network == null) errors.Add("Network transport adapter is missing.");
                if (bindings.Matchmaking == null) errors.Add("Matchmaking/session adapter is missing.");
                if (bindings.Profiles == null) errors.Add("Profile persistence adapter is missing.");
                if (bindings.Health == null) errors.Add("Backend health adapter is missing.");
                if (bindings.Matchmaking is UnavailableMatchmakingService) errors.Add("Unavailable development matchmaking cannot be used in Production.");
                if (bindings.Profiles is UnavailableProductionProfileApi) errors.Add("Unavailable development profile storage cannot be used in Production.");
            }
            return new ProductionIntegrationReport(errors);
        }

        private static bool IsPlaceholder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var id=value.Trim();
            return id.Length < 8 || id.Equals("TODO", StringComparison.OrdinalIgnoreCase) ||
                id.IndexOf("your", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("change", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("example", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.Equals("00000000-0000-0000-0000-000000000000", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafeId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value.All(character =>
            (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
            (character >= '0' && character <= '9') || character == '-' || character == '_' || character == '.');
    }

    /// <summary>Production service composition root. It cannot be created until the release gate accepts real adapters.</summary>
    public sealed class ProductionRuntimeServices
    {
        public EnvironmentConfig Environment { get; }
        public ClientVersionInfo Version { get; }
        public BuildMetadata Build { get; }
        public FrozenProductionConfiguration FrozenConfiguration { get; }
        public IAuthenticatedPlayer Authentication { get; }
        public GameNetworkManager Network { get; }
        public IMatchmakingService Matchmaking { get; }
        public ProductionProfileGateway Profiles { get; }
        public IBackendHealthProbe Health { get; }
        public BuildProfileKind Profile { get; }
        public BuildProfileSettings ProfileSettings { get; }

        private ProductionRuntimeServices(ProductionIntegrationManifest manifest, ProductionServiceBindings bindings)
        {
            Environment=manifest.Environment; Version=manifest.Version; Build=manifest.Build; FrozenConfiguration=manifest.FrozenConfiguration; Authentication=bindings.Authentication;
            Network=new GameNetworkManager(bindings.Network); Matchmaking=bindings.Matchmaking;
            Profiles=new ProductionProfileGateway(bindings.Authentication, bindings.Profiles); Health=bindings.Health;
            Profile=BuildProfileKind.Release; ProfileSettings=BuildProfileSettings.For(Profile);
        }

        /// <summary>Creates the session-scoped gameplay composition after matchmaking has supplied a validated roster.</summary>
        public GameComposition CreateGameplay(GameRuntimeConfiguration configuration, IScenarioGenerator scenarioGenerator,
            IEnumerable<IGameCompositionModule> hostModules = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (scenarioGenerator == null) throw new ArgumentNullException(nameof(scenarioGenerator));
            var modules=new IGameCompositionModule[] { new ProductionBindingsModule(this) }.Concat(hostModules ?? Enumerable.Empty<IGameCompositionModule>());
            return new GameCompositionRoot().Build(configuration, scenarioGenerator, modules: modules);
        }

        public static ProductionRuntimeServices Compose(ProductionIntegrationManifest manifest, ProductionServiceBindings bindings)
        {
            var report=ProductionIntegrationGate.Validate(manifest, bindings);
            if (!report.IsReady) throw new InvalidOperationException("Production composition is blocked: " + string.Join("; ", report.Errors));
            return new ProductionRuntimeServices(manifest, bindings);
        }

        private sealed class ProductionBindingsModule : IGameCompositionModule
        {
            private readonly ProductionRuntimeServices owner;
            public ProductionBindingsModule(ProductionRuntimeServices owner) { this.owner=owner; }
            public void Register(IServiceRegistry services)
            {
                services.RegisterBorrowed<EnvironmentConfig>(owner.Environment);
                services.RegisterBorrowed<ClientVersionInfo>(owner.Version);
                services.RegisterBorrowed<BuildMetadata>(owner.Build);
                services.RegisterBorrowed<FrozenProductionConfiguration>(owner.FrozenConfiguration);
                services.RegisterBorrowed<IAuthenticatedPlayer>(owner.Authentication);
                services.RegisterBorrowed<GameNetworkManager>(owner.Network);
                services.RegisterBorrowed<IMatchmakingService>(owner.Matchmaking);
                services.RegisterBorrowed<ProductionProfileGateway>(owner.Profiles);
                services.RegisterBorrowed<IBackendHealthProbe>(owner.Health);
                var runtime=services.Resolve<CoreGameRuntime>();
                services.Register<VersionedMatchContract>(new VersionedMatchContract(runtime.State.Match.MatchId,
                    owner.Build, owner.Build.RulesVersion, owner.Build.ScenarioVersion, runtime.State.Match.Seed));
            }
        }
    }
}
