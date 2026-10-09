using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Backend
{
    public enum TelemetryEventType { MatchStarted, RoundStarted, DiscussionEnded, VoteResolved, MatchCompleted, MatchmakingFailed, ClientError, PlayerReported }

    /// <summary>Allowlisted operational fields only. No chat, clue text, private role, or hidden timeline fields.</summary>
    [Serializable]
    public sealed class TelemetryEvent
    {
        public string eventId;
        public TelemetryEventType type;
        public string sessionId;
        public string matchId;
        public string scenarioId;
        public int contentVersion;
        public int playerCount;
        public int roundNumber;
        public int durationSeconds;
        public string resultCode;
        public long occurredAtUtcTicks;
    }

    public interface ITelemetrySink
    {
        // The sink acknowledges IDs only after durable acceptance.
        ISet<string> Send(IReadOnlyList<TelemetryEvent> events);
    }

    public sealed class UnavailableTelemetrySink : ITelemetrySink
    {
        public ISet<string> Send(IReadOnlyList<TelemetryEvent> events) => new HashSet<string>(StringComparer.Ordinal);
    }

    public sealed class TelemetryQueue
    {
        private readonly int _capacity;
        private readonly Queue<TelemetryEvent> _queue = new Queue<TelemetryEvent>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);
        public TelemetryQueue(int capacity = 500)
        { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }

        public bool TryEnqueue(TelemetryEvent item)
        {
            if (!IsAllowed(item) || _queue.Count >= _capacity || !_ids.Add(item.eventId)) return false;
            _queue.Enqueue(Clone(item)); return true;
        }

        public int Flush(ITelemetrySink sink, int batchSize = 50)
        {
            if (sink == null || batchSize < 1 || _queue.Count == 0) return 0;
            var batch = _queue.Take(batchSize).Select(Clone).ToList();
            var ack = sink.Send(batch.AsReadOnly()) ?? new HashSet<string>();
            var retained = new Queue<TelemetryEvent>(); var removed = 0;
            while (_queue.Count > 0)
            {
                var item = _queue.Dequeue();
                if (batch.Any(x => x.eventId == item.eventId) && ack.Contains(item.eventId)) { _ids.Remove(item.eventId); removed++; }
                else retained.Enqueue(item);
            }
            while (retained.Count > 0) _queue.Enqueue(retained.Dequeue());
            return removed;
        }

        private static bool IsAllowed(TelemetryEvent e) => e != null && !string.IsNullOrWhiteSpace(e.eventId) && Enum.IsDefined(typeof(TelemetryEventType), e.type) &&
            e.playerCount >= 0 && e.playerCount <= 6 && e.roundNumber >= 0 && e.roundNumber <= 5 && e.durationSeconds >= 0 && e.durationSeconds <= 3600 &&
            e.eventId.Length <= 80 && (e.sessionId == null || e.sessionId.Length <= 80) && (e.matchId == null || e.matchId.Length <= 80) &&
            (e.scenarioId == null || e.scenarioId.Length <= 80) && (e.resultCode == null || (e.resultCode.Length <= 40 && IsSafeCode(e.resultCode)));
        private static bool IsSafeCode(string text) => text.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-');
        private static TelemetryEvent Clone(TelemetryEvent e) => new TelemetryEvent { eventId=e.eventId, type=e.type, sessionId=e.sessionId, matchId=e.matchId, scenarioId=e.scenarioId, contentVersion=e.contentVersion, playerCount=e.playerCount, roundNumber=e.roundNumber, durationSeconds=e.durationSeconds, resultCode=e.resultCode, occurredAtUtcTicks=e.occurredAtUtcTicks };
    }

    public enum PlayerReportCategory { Cheating, Harassment, InappropriateName, Griefing, Other }
    [Serializable]
    public sealed class PlayerReport
    {
        public string reportId;
        public string reporterPlayerId;
        public string targetPlayerId;
        public string matchId;
        public PlayerReportCategory category;
        public long createdAtUtcTicks;
        // Intentionally no free-text field: don't collect chat or clue content in MVP reports.
    }
    public interface IPlayerModerationService
    {
        ServiceStatus Submit(PlayerReport report, string authenticatedPlayerId);
        ServiceStatus Block(string targetPlayerId, string authenticatedPlayerId);
        ServiceStatus Mute(string targetPlayerId, string authenticatedPlayerId);
    }

    public sealed class UnavailablePlayerModerationService : IPlayerModerationService
    {
        public ServiceStatus Submit(PlayerReport report, string authenticatedPlayerId) => ServiceStatus.Unavailable;
        public ServiceStatus Block(string targetPlayerId, string authenticatedPlayerId) => ServiceStatus.Unavailable;
        public ServiceStatus Mute(string targetPlayerId, string authenticatedPlayerId) => ServiceStatus.Unavailable;
    }

    /// <summary>Local client-side affordance only; backend moderation decisions remain provider-owned.</summary>
    public sealed class LocalBlockMuteList
    {
        private readonly HashSet<string> _blocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _muted = new HashSet<string>(StringComparer.Ordinal);
        public void Block(string playerId) { if (!string.IsNullOrWhiteSpace(playerId)) _blocked.Add(playerId); }
        public void Mute(string playerId) { if (!string.IsNullOrWhiteSpace(playerId)) _muted.Add(playerId); }
        public bool IsBlocked(string playerId) => _blocked.Contains(playerId);
        public bool IsMuted(string playerId) => _muted.Contains(playerId);
    }
}
