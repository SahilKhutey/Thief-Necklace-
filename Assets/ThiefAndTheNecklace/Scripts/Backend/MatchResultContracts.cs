using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ThiefAndTheNecklace.Backend
{
    [Serializable]
    public sealed class MatchResultRecord
    {
        public string matchId;
        public int resultSchemaVersion = 1;
        public string contentVersion;
        public List<RoundResultRecord> rounds = new List<RoundResultRecord>();
        public List<PlayerMatchResult> players = new List<PlayerMatchResult>();
    }

    [Serializable]
    public sealed class RoundResultRecord
    {
        public string roundId;
        public string scenarioId;
        public int scenarioVersion;
        public int seed;
        public string thiefPlayerId;
        public string accusedPlayerId;
        public bool tieAfterRunoff;
        public List<PlayerRoundResult> players = new List<PlayerRoundResult>();
    }

    [Serializable]
    public sealed class PlayerRoundResult
    {
        public string playerId;
        public string voteTargetPlayerId;
        public int rolePointValue;
        public int scoreDelta;
    }

    [Serializable]
    public sealed class PlayerMatchResult
    {
        public string playerId;
        public int totalScore;
    }

    public enum MatchResultFailure { None, Invalid, Conflict, Unavailable }
    public sealed class MatchResultValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class MatchResultValidator
    {
        public static MatchResultValidation Validate(MatchResultRecord match)
        {
            var result = new MatchResultValidation();
            if (match == null) { result.Errors.Add("Result is missing."); return result; }
            if (string.IsNullOrWhiteSpace(match.matchId) || match.resultSchemaVersion != 1 || string.IsNullOrWhiteSpace(match.contentVersion))
                result.Errors.Add("Match identity, supported schema, and content version are required.");
            var players = (match.players ?? new List<PlayerMatchResult>()).Where(x => x != null).ToList();
            var ids = new HashSet<string>(players.Select(x => x.playerId), StringComparer.Ordinal);
            if (players.Count < 4 || players.Count > 6 || ids.Count != players.Count || ids.Contains(null) || ids.Contains(string.Empty))
                result.Errors.Add("A result needs 4–6 players with unique nonempty IDs.");
            if (players.Any(x => x == null || string.IsNullOrWhiteSpace(x.playerId)) || ids.Count != players.Count || players.Count < 4 || players.Count > 6)
                return result;
            if (match.rounds == null || match.rounds.Count != 5) result.Errors.Add("A completed MVP match must contain five rounds.");
            var roundIds = new HashSet<string>(StringComparer.Ordinal);
            var totals = players.ToDictionary(x => x.playerId, x => 0, StringComparer.Ordinal);
            foreach (var round in match.rounds ?? new List<RoundResultRecord>())
            {
                if (round == null || string.IsNullOrWhiteSpace(round.roundId) || !roundIds.Add(round.roundId) || string.IsNullOrWhiteSpace(round.scenarioId) || round.scenarioVersion <= 0)
                { result.Errors.Add("Every round requires unique identity and a versioned scenario."); continue; }
                var outcomes = (round.players ?? new List<PlayerRoundResult>()).Where(x => x != null).ToList();
                if (outcomes.Count != ids.Count || outcomes.Select(x => x.playerId).Distinct(StringComparer.Ordinal).Count() != ids.Count || outcomes.Any(x => !ids.Contains(x.playerId)))
                { result.Errors.Add("Each round must contain exactly one outcome per match player."); continue; }
                if (!ids.Contains(round.thiefPlayerId) || (round.accusedPlayerId != null && !ids.Contains(round.accusedPlayerId)))
                { result.Errors.Add("Thief and accused IDs must belong to the roster."); continue; }
                var voteCounts = outcomes.Where(x => ids.Contains(x.voteTargetPlayerId) && x.voteTargetPlayerId != x.playerId)
                    .GroupBy(x => x.voteTargetPlayerId, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
                var highestVotes = voteCounts.Count == 0 ? 0 : voteCounts.Values.Max();
                var leaders = voteCounts.Where(x => x.Value == highestVotes).Select(x => x.Key).ToList();
                if (round.tieAfterRunoff ? (round.accusedPlayerId != null || leaders.Count < 2) : (leaders.Count != 1 || round.accusedPlayerId != leaders[0]))
                    result.Errors.Add("Accused player must match the final vote tally; a final tie has no accused player.");
                foreach (var p in outcomes)
                {
                    if (!ids.Contains(p.voteTargetPlayerId) || p.voteTargetPlayerId == p.playerId) result.Errors.Add("Votes must target another roster player.");
                    if (p.rolePointValue < 0 || p.rolePointValue > 1000) result.Errors.Add("Role point values must be bounded and nonnegative.");
                    var expected = p.playerId == round.thiefPlayerId
                        ? (round.accusedPlayerId == round.thiefPlayerId ? 0 : outcomes.Where(x => x.playerId != p.playerId).Sum(x => x.rolePointValue))
                        : (p.voteTargetPlayerId == round.thiefPlayerId ? p.rolePointValue : 0);
                    if (p.scoreDelta != expected) result.Errors.Add("Round score delta does not match the published scoring rules.");
                    totals[p.playerId] += p.scoreDelta;
                }
            }
            foreach (var p in players) if (p.totalScore != totals[p.playerId]) result.Errors.Add("Match total does not equal the sum of round scores.");
            return result;
        }

        // Stable canonical representation; never hash serializer output or private clues.
        public static string ComputeCanonicalHash(MatchResultRecord match)
        {
            var b = new StringBuilder();
            Add(b, match.matchId); Add(b, match.resultSchemaVersion); Add(b, match.contentVersion);
            foreach (var round in (match.rounds ?? new List<RoundResultRecord>()).OrderBy(x => x.roundId, StringComparer.Ordinal))
            {
                Add(b, round.roundId); Add(b, round.scenarioId); Add(b, round.scenarioVersion); Add(b, round.seed);
                Add(b, round.thiefPlayerId); Add(b, round.accusedPlayerId); Add(b, round.tieAfterRunoff ? 1 : 0);
                foreach (var p in (round.players ?? new List<PlayerRoundResult>()).OrderBy(x => x.playerId, StringComparer.Ordinal))
                { Add(b, p.playerId); Add(b, p.voteTargetPlayerId); Add(b, p.rolePointValue); Add(b, p.scoreDelta); }
            }
            foreach (var p in (match.players ?? new List<PlayerMatchResult>()).OrderBy(x => x.playerId, StringComparer.Ordinal))
            { Add(b, p.playerId); Add(b, p.totalScore); }
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(b.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        private static void Add(StringBuilder b, string value) { var s = value ?? string.Empty; b.Append(s.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s).Append('|'); }
        private static void Add(StringBuilder b, int value) => Add(b, value.ToString(CultureInfo.InvariantCulture));
    }

    public interface IAuthoritativeCompletedMatchSource
    {
        bool TryGetCompleted(string matchId, out MatchResultRecord result);
    }
    public interface IMatchResultStore
    {
        // Atomic insert-if-absent. Existing identical hash is success; different hash is conflict.
        MatchResultFailure CommitIfAbsent(MatchResultRecord result, string canonicalHash);
    }

    /// <summary>Only fetches completed records from the trusted match authority. Clients cannot submit rewards/results.</summary>
    public sealed class AuthoritativeMatchResultService
    {
        private readonly IAuthoritativeCompletedMatchSource _source;
        private readonly IMatchResultStore _store;
        public AuthoritativeMatchResultService(IAuthoritativeCompletedMatchSource source, IMatchResultStore store)
        { _source = source ?? throw new ArgumentNullException(nameof(source)); _store = store ?? throw new ArgumentNullException(nameof(store)); }
        public MatchResultFailure Finalize(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId) || !_source.TryGetCompleted(matchId, out var result)) return MatchResultFailure.Invalid;
            var validation = MatchResultValidator.Validate(result);
            if (!validation.IsValid || !string.Equals(result.matchId, matchId, StringComparison.Ordinal)) return MatchResultFailure.Invalid;
            return _store.CommitIfAbsent(result, MatchResultValidator.ComputeCanonicalHash(result));
        }
    }
}
