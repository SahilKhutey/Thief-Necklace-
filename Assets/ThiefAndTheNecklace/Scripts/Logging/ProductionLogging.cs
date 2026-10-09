using System;
using System.Linq;
using System.Threading;
using ThiefAndTheNecklace.Backend;

namespace ThiefAndTheNecklace.Logging
{
    public enum ProductionLogLevel { Trace, Debug, Info, Warn, Error, Fatal }
    public enum ProductionLogArea { Boot, Auth, Session, Lobby, Match, Scenario, PrivateDelivery, Discussion, Chat, Vote, Reveal, Score, Reward, Profile, Cosmetics, Achievement, Progression, Matchmaking, Party, Social, Moderation, Reconnect, Performance, Memory, Network, Error, Incident, Release, Configuration, FeatureFlag, Security, Audit, Test }
    public enum ProductionLogEvent { ApplicationStarting, ApplicationReady, VersionRejected, SessionFailed, MatchStarted, ScenarioGenerated, ScenarioValidationFailed, PrivateStateDelivered, DiscussionStarted, DiscussionEnded, ChatAccepted, VoteRejected, VoteAccepted, VoteResolved, RevealStarted, ScoreCommitted, RewardCommitted, DuplicateRewardRejected, ProfileLoadFailed, ProfileSaveConflict, QueueEntered, QueueFailed, ReconnectSucceeded, HostLost, DependencyDegraded, ContentStaged, ContentActivated, ContentRollback, PolicyRejected, TestRunStarted, TestRunFinished, BuildArchived, IncidentOpened, IncidentResolved }
    public enum ProductionLogOutcome { None, Success, Rejected, Failed, Retry, Conflict, Unavailable, Completed }

    /// <summary>
    /// Allowlisted structured record. It deliberately has no message, player ID, vote target,
    /// role, clue, chat, token, request-body, or world-truth field.
    /// </summary>
    public sealed class SafeProductionLogRecord
    {
        public string EventId { get; private set; }
        public long TimestampUtcTicks { get; private set; }
        public ProductionLogLevel Level { get; private set; }
        public ProductionLogArea Area { get; private set; }
        public ProductionLogEvent Event { get; private set; }
        public ProductionLogOutcome Outcome { get; private set; }
        public EnvironmentType Environment { get; private set; }
        public string BuildVersion { get; private set; }
        public string CorrelationId { get; private set; }
        public string MatchId { get; private set; }
        public string RoundId { get; private set; }
        public string ScenarioId { get; private set; }
        public int? DurationMilliseconds { get; private set; }
        public int? Count { get; private set; }

        internal SafeProductionLogRecord(string eventId, long timestampUtcTicks, ProductionLogLevel level, ProductionLogArea area,
            ProductionLogEvent eventCode, ProductionLogOutcome outcome, EnvironmentType environment, string buildVersion,
            string correlationId, string matchId, string roundId, string scenarioId, int? durationMilliseconds, int? count)
        {
            EventId=eventId; TimestampUtcTicks=timestampUtcTicks; Level=level; Area=area; Event=eventCode; Outcome=outcome;
            Environment=environment; BuildVersion=buildVersion; CorrelationId=correlationId; MatchId=matchId;
            RoundId=roundId; ScenarioId=scenarioId; DurationMilliseconds=durationMilliseconds; Count=count;
        }
    }

    public interface IProductionLogClock { long UtcTicks { get; } }
    public interface IProductionLogSink { void Write(SafeProductionLogRecord record); }

    /// <summary>Structured allowlist logger. Production drops TRACE/DEBUG; sinks must also enforce access and retention.</summary>
    public sealed class ProductionLogger
    {
        private readonly EnvironmentConfig _environment;
        private readonly string _buildVersion;
        private readonly IProductionLogClock _clock;
        private readonly IProductionLogSink _sink;
        private int _sinkFailureCount;
        public int SinkFailureCount { get { return Volatile.Read(ref _sinkFailureCount); } }
        public ProductionLogger(EnvironmentConfig environment, string buildVersion, IProductionLogClock clock, IProductionLogSink sink)
        {
            _environment=environment ?? throw new ArgumentNullException(nameof(environment));
            if (!IsSafeId(buildVersion)) throw new ArgumentException("Build version must be a bounded identifier.", nameof(buildVersion));
            _buildVersion=buildVersion; _clock=clock ?? throw new ArgumentNullException(nameof(clock)); _sink=sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool TryWrite(ProductionLogLevel level, ProductionLogArea area, ProductionLogEvent eventCode,
            ProductionLogOutcome outcome = ProductionLogOutcome.None, string correlationId = null, string matchId = null,
            string roundId = null, string scenarioId = null, int? durationMilliseconds = null, int? count = null)
        {
            if (!Enum.IsDefined(typeof(ProductionLogLevel), level) || !Enum.IsDefined(typeof(ProductionLogArea), area) ||
                !Enum.IsDefined(typeof(ProductionLogEvent), eventCode) || !Enum.IsDefined(typeof(ProductionLogOutcome), outcome)) return false;
            if (_environment.Environment == EnvironmentType.Production && (level == ProductionLogLevel.Trace || level == ProductionLogLevel.Debug)) return false;
            if (!IsOptionalSafeId(correlationId) || !IsOptionalSafeId(matchId) || !IsOptionalSafeId(roundId) || !IsOptionalSafeId(scenarioId)) return false;
            if ((durationMilliseconds.HasValue && (durationMilliseconds.Value < 0 || durationMilliseconds.Value > 86400000)) ||
                (count.HasValue && (count.Value < 0 || count.Value > 100000000))) return false;
            var ticks=_clock.UtcTicks;
            if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return false;
            var record=new SafeProductionLogRecord(Guid.NewGuid().ToString("N"), ticks, level, area, eventCode, outcome,
                _environment.Environment, _buildVersion, correlationId, matchId, roundId, scenarioId, durationMilliseconds, count);
            try { _sink.Write(record); return true; }
            catch { Interlocked.Increment(ref _sinkFailureCount); return false; } // Logging must never break the game flow.
        }

        private static bool IsOptionalSafeId(string value) => value == null || IsSafeId(value);
        private static bool IsSafeId(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 && value.All(c =>
            (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.');
    }
}
