using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ThiefAndTheNecklace.Backend
{
    public enum ReleaseChannel { Development, Qa, Staging, Production }
    public enum ReleaseState { Development, Qa, Staging, Candidate, Deploying, Live, Degraded, Rollback, Maintenance, Retired }
    public enum IncidentSeverity { P0, P1, P2, P3 }
    public enum IncidentStatus { Open, Investigating, Mitigating, Resolved, Closed }
    public enum AnalyticsEventKind { AppStarted, SessionJoined, MatchStarted, MatchCompleted, MatchAborted, ReconnectSucceeded, ReconnectFailed, VoteSubmitted, PhaseDuration, ClientError }
    public enum AnalyticsResultCode { None, Success, Failure, Timeout, Rejected, Cancelled, Unavailable, ProtocolMismatch }

    public sealed class VersionedMatchContract
    {
        public string MatchId { get; }
        public string BuildVersion { get; }
        public int BuildNumber { get; }
        public int ProtocolVersion { get; }
        public int RulesVersion { get; }
        public int ScenarioVersion { get; }
        private readonly int authoritySeed;

        public VersionedMatchContract(string matchId, BuildMetadata build, int rulesVersion, int scenarioVersion, int seed)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("A match ID is required.", nameof(matchId));
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (build.Validate().Count != 0) throw new ArgumentException("Build metadata must be complete before creating a match contract.", nameof(build));
            if (rulesVersion != build.RulesVersion || scenarioVersion != build.ScenarioVersion)
                throw new ArgumentException("Match rules and scenario versions must match the creating build.", nameof(rulesVersion));
            MatchId=matchId; BuildVersion=build.GameVersion; BuildNumber=build.BuildNumber;
            ProtocolVersion=build.NetworkProtocolVersion; RulesVersion=rulesVersion; ScenarioVersion=scenarioVersion; authoritySeed=seed;
            if (BuildNumber <= 0 || ProtocolVersion <= 0 || RulesVersion <= 0 || ScenarioVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(build), "A match requires positive build, protocol, rules, and scenario versions.");
        }

        public bool IsCompatible(VersionedMatchContract client) => client != null &&
            string.Equals(BuildVersion, client.BuildVersion, StringComparison.Ordinal) &&
            BuildNumber == client.BuildNumber &&
            ProtocolVersion == client.ProtocolVersion && RulesVersion == client.RulesVersion && ScenarioVersion == client.ScenarioVersion;

        internal int GetAuthorityReplaySeed() => authoritySeed;
    }

    /// <summary>Immutable runtime config snapshot with a stable digest for release audit and match pinning.</summary>
    public sealed class FrozenProductionConfiguration
    {
        private readonly Dictionary<string, string> values;
        public ReleaseChannel Channel { get; }
        public string Digest { get; }
        public IReadOnlyDictionary<string, string> Values => new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(values);

        public FrozenProductionConfiguration(ReleaseChannel channel, IEnumerable<KeyValuePair<string, string>> values)
        {
            if (!Enum.IsDefined(typeof(ReleaseChannel), channel)) throw new ArgumentOutOfRangeException(nameof(channel));
            Channel=channel;
            this.values=new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in values ?? Enumerable.Empty<KeyValuePair<string, string>>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("Configuration keys must be named.", nameof(values));
                if (ContainsCredentialMarker(pair.Key)) throw new ArgumentException("Secret material must be supplied by a secure provider, not frozen into release configuration.", nameof(values));
                if (this.values.ContainsKey(pair.Key)) throw new ArgumentException("Configuration keys must be unique.", nameof(values));
                this.values.Add(pair.Key, pair.Value ?? string.Empty);
            }
            var canonical=new StringBuilder().Append((int)channel).Append('|');
            foreach (var pair in this.values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                canonical.Append(pair.Key.Length).Append(':').Append(pair.Key).Append(pair.Value.Length).Append(':').Append(pair.Value);
            using (var sha=SHA256.Create()) Digest=ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
        }

        private static string ToHex(byte[] bytes)
        {
            var result=new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) result.Append(value.ToString("x2"));
            return result.ToString();
        }
        private static bool ContainsCredentialMarker(string key)
        {
            var normalized=key.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
            return normalized.Contains("secret") || normalized.Contains("password") || normalized.Contains("credential") ||
                normalized.Contains("token") || normalized.Contains("apikey") || normalized.Contains("privatekey") || normalized.Contains("signingkey");
        }
    }

    public sealed class FeatureFlag
    {
        public string Name { get; }
        public bool Enabled { get; }
        public int RolloutPercent { get; }
        public FeatureFlag(string name, bool enabled, int rolloutPercent)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Flag name is required.", nameof(name));
            if (rolloutPercent < 0 || rolloutPercent > 100) throw new ArgumentOutOfRangeException(nameof(rolloutPercent));
            Name=name; Enabled=enabled; RolloutPercent=rolloutPercent;
        }
    }

    /// <summary>Flags are evaluated by deterministic anonymous bucket key; flags never participate in domain outcomes.</summary>
    public sealed class FeatureFlagSet
    {
        private readonly Dictionary<string, FeatureFlag> flags;
        public FeatureFlagSet(IEnumerable<FeatureFlag> flags)
        {
            this.flags=new Dictionary<string, FeatureFlag>(StringComparer.Ordinal);
            foreach (var flag in flags ?? Enumerable.Empty<FeatureFlag>())
            {
                if (flag == null) continue;
                if (this.flags.ContainsKey(flag.Name)) throw new ArgumentException("Feature flag names must be unique.", nameof(flags));
                this.flags.Add(flag.Name, flag);
            }
        }
        public bool IsEnabled(string name, string anonymousBucketKey)
        {
            FeatureFlag flag;
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(anonymousBucketKey) || !flags.TryGetValue(name, out flag) || !flag.Enabled || flag.RolloutPercent == 0) return false;
            if (flag.RolloutPercent == 100) return true;
            var bytes=Encoding.UTF8.GetBytes(flag.Name + ":" + anonymousBucketKey);
            using (var sha=SHA256.Create())
            {
                var hash=sha.ComputeHash(bytes);
                var bucket=((hash[0] << 8) | hash[1]) % 100;
                return bucket < flag.RolloutPercent;
            }
        }
    }

    public sealed class SafeAnalyticsEvent
    {
        public AnalyticsEventKind Kind { get; }
        public DateTime TimestampUtc { get; }
        public string BuildVersion { get; }
        public string Platform { get; }
        public int Count { get; }
        public double DurationMilliseconds { get; }
        public AnalyticsResultCode ResultCode { get; }
        /// <summary>No player ID, role, vote target, chat, evidence, seed, or arbitrary payload is accepted.</summary>
        public SafeAnalyticsEvent(AnalyticsEventKind kind, DateTime timestampUtc, string buildVersion,
            string platform, int count = 0, double durationMilliseconds = 0, AnalyticsResultCode resultCode = AnalyticsResultCode.None)
        {
            if (!Enum.IsDefined(typeof(AnalyticsEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (count < 0 || double.IsNaN(durationMilliseconds) || double.IsInfinity(durationMilliseconds) || durationMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!Enum.IsDefined(typeof(AnalyticsResultCode), resultCode)) throw new ArgumentOutOfRangeException(nameof(resultCode));
            Kind=kind; TimestampUtc=timestampUtc.Kind == DateTimeKind.Utc ? timestampUtc : timestampUtc.ToUniversalTime();
            BuildVersion=SafeToken(buildVersion, nameof(buildVersion)); Platform=SafeToken(platform, nameof(platform));
            Count=count; DurationMilliseconds=durationMilliseconds; ResultCode=resultCode;
        }
        private static string SafeToken(string value, string name)
        {
            var token=value ?? string.Empty;
            if (token.Length > 32 || token.Any(character => !((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') || character == '.' || character == '-' || character == '_')))
                throw new ArgumentException("Analytics dimensions must be short, preapproved tokens.", name);
            return token;
        }
    }

    /// <summary>Bounded, nonblocking producer queue. Sink work is explicitly drained by the host's background worker.</summary>
    public sealed class SafeAnalyticsQueue
    {
        private readonly object gate=new object();
        private readonly Queue<SafeAnalyticsEvent> queue=new Queue<SafeAnalyticsEvent>();
        private readonly int capacity;
        private long droppedCount;
        public long DroppedCount { get { lock (gate) return droppedCount; } }
        public SafeAnalyticsQueue(int capacity = 512) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); this.capacity=capacity; }
        public bool TryEnqueue(SafeAnalyticsEvent item)
        {
            if (item == null) return false;
            lock (gate)
            {
                if (queue.Count >= capacity) { droppedCount++; return false; }
                queue.Enqueue(item); return true;
            }
        }
        public bool TryDequeue(out SafeAnalyticsEvent item)
        {
            lock (gate) { if (queue.Count == 0) { item=null; return false; } item=queue.Dequeue(); return true; }
        }
        public void Clear() { lock (gate) queue.Clear(); }
    }

    public sealed class ProductionIncident
    {
        public string IncidentId { get; }
        public IncidentSeverity Severity { get; }
        public IncidentStatus Status { get; }
        public string CategoryCode { get; }
        public DateTime OpenedUtc { get; }
        public ProductionIncident(string incidentId, IncidentSeverity severity, IncidentStatus status, string categoryCode, DateTime openedUtc)
        {
            if (string.IsNullOrWhiteSpace(incidentId) || incidentId.Length > 64 || incidentId.Any(character => !((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9') || character == '-' || character == '_')))
                throw new ArgumentException("Incident ID must be a bounded safe token.", nameof(incidentId));
            if (!Enum.IsDefined(typeof(IncidentSeverity), severity) || !Enum.IsDefined(typeof(IncidentStatus), status)) throw new ArgumentOutOfRangeException(nameof(severity));
            IncidentId=incidentId; Severity=severity; Status=status;
            CategoryCode=new string((categoryCode ?? string.Empty).Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-').Take(48).ToArray());
            OpenedUtc=openedUtc.Kind == DateTimeKind.Utc ? openedUtc : openedUtc.ToUniversalTime();
        }
        public bool BlocksRelease => (Severity == IncidentSeverity.P0 || Severity == IncidentSeverity.P1) &&
            Status != IncidentStatus.Resolved && Status != IncidentStatus.Closed;
    }

    /// <summary>Small in-memory incident ledger contract. A production host must persist this through its approved operations system.</summary>
    public sealed class IncidentLedger
    {
        private readonly object gate=new object();
        private readonly Dictionary<string, ProductionIncident> incidents=new Dictionary<string, ProductionIncident>(StringComparer.Ordinal);
        public void Open(ProductionIncident incident)
        {
            if (incident == null) throw new ArgumentNullException(nameof(incident));
            lock (gate) { if (incidents.ContainsKey(incident.IncidentId)) throw new InvalidOperationException("Incident ID already exists."); incidents.Add(incident.IncidentId, incident); }
        }
        public ProductionIncident Get(string incidentId)
        { lock (gate) { ProductionIncident value; return incidents.TryGetValue(incidentId ?? string.Empty, out value) ? value : null; } }
        public IReadOnlyList<ProductionIncident> Snapshot()
        { lock (gate) return Array.AsReadOnly(incidents.Values.OrderBy(item => item.OpenedUtc).ToArray()); }
        public ProductionIncident Transition(string incidentId, IncidentStatus next)
        {
            lock (gate)
            {
                ProductionIncident current;
                if (!incidents.TryGetValue(incidentId ?? string.Empty, out current)) throw new KeyNotFoundException("Incident does not exist.");
                if (!CanTransition(current.Status, next)) throw new InvalidOperationException("Invalid incident status transition: " + current.Status + " → " + next + ".");
                var updated=new ProductionIncident(current.IncidentId, current.Severity, next, current.CategoryCode, current.OpenedUtc);
                incidents[updated.IncidentId]=updated; return updated;
            }
        }
        public int CountBlocking()
        { lock (gate) return incidents.Values.Count(item => item.BlocksRelease); }
        private static bool CanTransition(IncidentStatus current, IncidentStatus next)
        {
            if (current == next) return true;
            switch (current)
            {
                case IncidentStatus.Open: return next == IncidentStatus.Investigating || next == IncidentStatus.Mitigating;
                case IncidentStatus.Investigating: return next == IncidentStatus.Mitigating || next == IncidentStatus.Resolved;
                case IncidentStatus.Mitigating: return next == IncidentStatus.Investigating || next == IncidentStatus.Resolved;
                case IncidentStatus.Resolved: return next == IncidentStatus.Closed || next == IncidentStatus.Investigating;
                default: return false;
            }
        }
    }

    public sealed class ProductionHealth
    {
        public ReleaseState ReleaseState { get; }
        public double ConnectionSuccessRate { get; }
        public double ReconnectSuccessRate { get; }
        public double CrashRate { get; }
        public int ActiveIncidents { get; }
        public bool GameplayIntegrityHealthy { get; }
        public ProductionHealth(ReleaseState releaseState, double connectionSuccessRate, double reconnectSuccessRate,
            double crashRate, int activeIncidents, bool gameplayIntegrityHealthy)
        {
            if (!Enum.IsDefined(typeof(ReleaseState), releaseState)) throw new ArgumentOutOfRangeException(nameof(releaseState));
            ValidateRate(connectionSuccessRate, nameof(connectionSuccessRate));
            ValidateRate(reconnectSuccessRate, nameof(reconnectSuccessRate));
            ValidateRate(crashRate, nameof(crashRate));
            if (activeIncidents < 0) throw new ArgumentOutOfRangeException(nameof(activeIncidents));
            ReleaseState=releaseState; ConnectionSuccessRate=connectionSuccessRate; ReconnectSuccessRate=reconnectSuccessRate;
            CrashRate=crashRate; ActiveIncidents=activeIncidents; GameplayIntegrityHealthy=gameplayIntegrityHealthy;
        }
        private static void ValidateRate(double value, string name)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(name); }
    }

    public sealed class ReleaseValidationEvidence
    {
        public bool CandidateBuilt { get; set; }
        public bool IntegrationPassed { get; set; }
        public bool RegressionPassed { get; set; }
        public bool MultiplayerPassed { get; set; }
        public bool RecoveryPassed { get; set; }
        public bool SecurityPassed { get; set; }
        public bool PlatformPassed { get; set; }
        public bool PerformancePassed { get; set; }
        public bool ConfigurationFrozen { get; set; }
        public bool RollbackAvailable { get; set; }
        public bool MonitoringReady { get; set; }
        public bool GameplayIntegrityHealthy { get; set; }
        public int P0Incidents { get; set; }
        public int CriticalP1Incidents { get; set; }
        public ReleaseValidationEvidence Copy() => new ReleaseValidationEvidence
        {
            CandidateBuilt=CandidateBuilt, IntegrationPassed=IntegrationPassed, RegressionPassed=RegressionPassed,
            MultiplayerPassed=MultiplayerPassed, RecoveryPassed=RecoveryPassed, SecurityPassed=SecurityPassed,
            PlatformPassed=PlatformPassed, PerformancePassed=PerformancePassed, ConfigurationFrozen=ConfigurationFrozen,
            RollbackAvailable=RollbackAvailable, MonitoringReady=MonitoringReady, GameplayIntegrityHealthy=GameplayIntegrityHealthy,
            P0Incidents=P0Incidents, CriticalP1Incidents=CriticalP1Incidents
        };
    }

    public static class ReleaseCandidateGate
    {
        public static IReadOnlyList<string> Validate(BuildMetadata build, ReleaseValidationEvidence evidence)
        {
            var errors=new List<string>();
            if (build == null) errors.Add("Build metadata is missing."); else errors.AddRange(build.Validate());
            if (evidence == null) return errors.Concat(new[] { "Release validation evidence is missing." }).ToArray();
            if (!evidence.CandidateBuilt) errors.Add("Release candidate artifact is missing.");
            if (!evidence.IntegrationPassed) errors.Add("GW15 production integration gate has not passed.");
            if (!evidence.RegressionPassed) errors.Add("Regression validation has not passed.");
            if (!evidence.MultiplayerPassed) errors.Add("Multiplayer validation has not passed.");
            if (!evidence.RecoveryPassed) errors.Add("Recovery validation has not passed.");
            if (!evidence.SecurityPassed) errors.Add("Security validation has not passed.");
            if (!evidence.PlatformPassed) errors.Add("Platform validation has not passed.");
            if (!evidence.PerformancePassed) errors.Add("Performance validation has not passed.");
            if (!evidence.ConfigurationFrozen) errors.Add("Production configuration is not frozen.");
            if (!evidence.RollbackAvailable) errors.Add("A compatible rollback target is unavailable.");
            if (!evidence.MonitoringReady) errors.Add("Production monitoring is not ready.");
            if (!evidence.GameplayIntegrityHealthy) errors.Add("Gameplay integrity is unhealthy.");
            if (evidence.P0Incidents > 0) errors.Add("P0 incidents block release.");
            if (evidence.CriticalP1Incidents > 0) errors.Add("Critical P1 incidents block release.");
            if (evidence.P0Incidents < 0 || evidence.CriticalP1Incidents < 0) errors.Add("Incident counts cannot be negative.");
            return errors.AsReadOnly();
        }
    }

    /// <summary>Deployment state transitions are explicit; rollback is complete only after target validation smoke passes.</summary>
    public sealed class ReleaseController
    {
        private readonly BuildMetadata build;
        private readonly ReleaseValidationEvidence evidence;
        public ReleaseState State { get; private set; } = ReleaseState.Candidate;
        public ReleaseController(BuildMetadata build, ReleaseValidationEvidence evidence)
        { this.build=build; this.evidence=evidence?.Copy(); }
        public void BeginDeployment()
        {
            Require(ReleaseState.Candidate);
            var errors=ReleaseCandidateGate.Validate(build, evidence);
            if (errors.Count > 0) throw new InvalidOperationException("Release candidate is blocked: " + string.Join("; ", errors));
            State=ReleaseState.Deploying;
        }
        public void MarkLive(bool smokePassed) { Require(ReleaseState.Deploying); if (!smokePassed) throw new InvalidOperationException("Deployment smoke must pass before release is Live."); State=ReleaseState.Live; }
        public void MarkDegraded() { if (State != ReleaseState.Live && State != ReleaseState.Deploying) throw new InvalidOperationException("Only a live or deploying release can be marked degraded."); State=ReleaseState.Degraded; }
        public void BeginRollback() { if (State != ReleaseState.Degraded && State != ReleaseState.Live && State != ReleaseState.Deploying) throw new InvalidOperationException("Release is not in a rollback-capable state."); State=ReleaseState.Rollback; }
        public void CompleteRollback(bool rollbackTargetCompatible, bool smokePassed)
        {
            Require(ReleaseState.Rollback);
            if (!rollbackTargetCompatible) throw new InvalidOperationException("Rollback target is incompatible with active backend/client contracts.");
            if (!smokePassed) throw new InvalidOperationException("Rollback smoke must pass before rollback is complete.");
            State=ReleaseState.Live;
        }
        public void EnterMaintenance() { if (State != ReleaseState.Live && State != ReleaseState.Degraded) throw new InvalidOperationException("Maintenance requires a live or degraded release."); State=ReleaseState.Maintenance; }
        public void ResumeFromMaintenance(bool healthChecksPassed) { Require(ReleaseState.Maintenance); if (!healthChecksPassed) throw new InvalidOperationException("Maintenance cannot end until health checks pass."); State=ReleaseState.Live; }
        public void Retire() { if (State != ReleaseState.Maintenance && State != ReleaseState.Live && State != ReleaseState.Degraded) throw new InvalidOperationException("Release cannot be retired from its current state."); State=ReleaseState.Retired; }
        private void Require(ReleaseState expected) { if (State != expected) throw new InvalidOperationException("Expected release state " + expected + ", current state is " + State + "."); }
    }

    public sealed class PostDeploymentValidation
    {
        public bool FiveMinuteCheckPassed { get; private set; }
        public bool ThirtyMinuteCheckPassed { get; private set; }
        public bool OneHourCheckPassed { get; private set; }
        public bool Complete => FiveMinuteCheckPassed && ThirtyMinuteCheckPassed && OneHourCheckPassed;
        public void RecordFiveMinute(bool passed) { FiveMinuteCheckPassed=passed; }
        public void RecordThirtyMinute(bool passed) { if (!FiveMinuteCheckPassed) throw new InvalidOperationException("The five-minute validation must be recorded first."); ThirtyMinuteCheckPassed=passed; }
        public void RecordOneHour(bool passed) { if (!ThirtyMinuteCheckPassed) throw new InvalidOperationException("The thirty-minute validation must be recorded first."); OneHourCheckPassed=passed; }
    }
}
