using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ThiefAndTheNecklace.PB1
{
    [Serializable]
    public sealed class GameSnapshot
    {
        public string MatchId = string.Empty;
        public int Seed;
        public int Round;
        public string RoundId = string.Empty;
        public RoundStatus RoundStatus;
        public RoundPhase Phase;
        public string ScenarioId = string.Empty;
        public int VoteRound;
        public string Resolution = string.Empty;
        public bool Completed;
        public RevealSnapshot Reveal;
        public CompletedRoundResult RoundResult;
        public MatchResult MatchResult;
        public List<PlayerScoreSnapshot> Scores = new List<PlayerScoreSnapshot>();
    }

    [Serializable]
    public sealed class PlayerScoreSnapshot
    {
        public int PlayerId;
        public int RoundScore;
        public int MatchScore;
    }

    public sealed class SnapshotService
    {
        public GameSnapshot Capture(GameRuntimeState state)
        {
            if (state == null || state.Match == null || state.Match.CurrentRoundState == null) throw new ArgumentNullException(nameof(state));
            return new GameSnapshot
            {
                MatchId=state.Match.MatchId, Seed=state.Match.Seed, Round=state.Match.CurrentRound,
                RoundId=state.Match.CurrentRoundState.RoundId, RoundStatus=state.Match.CurrentRoundState.Status,
                Phase=state.Match.CurrentRoundState.Phase, ScenarioId=state.Match.CurrentRoundState.ScenarioId,
                VoteRound=state.Match.CurrentRoundState.VoteRound, Resolution=state.Match.CurrentRoundState.Resolution.ToString(),
                Completed=state.Match.CurrentRoundState.Completed,
                Scores=state.Match.Players.Select(p => new PlayerScoreSnapshot { PlayerId=p.PlayerId, RoundScore=p.RoundScore, MatchScore=p.MatchScore }).ToList()
            };
        }
    }

    [Serializable]
    public sealed class ReplayRecord
    {
        public int SchemaVersion = 1;
        public string RulesVersion = "tn-rules-1";
        public string ScenarioVersion = "unversioned";
        public bool ContainsAuthorityPrivateData;
        public bool SeedRedacted;
        public string FinalHash = ReplayIntegrity.GenesisHash;
        public string MatchId = string.Empty;
        public int Seed;
        public List<ReplayEvent> Events = new List<ReplayEvent>();
        public ReplayRecord DeepCopy() => new ReplayRecord
        {
            SchemaVersion=SchemaVersion, RulesVersion=RulesVersion, ScenarioVersion=ScenarioVersion,
            ContainsAuthorityPrivateData=ContainsAuthorityPrivateData,
            SeedRedacted=SeedRedacted,
            FinalHash=FinalHash,
            MatchId=MatchId, Seed=Seed,
            Events=Events == null ? null : Events.Select(e => e == null ? null : e.DeepCopy()).ToList()
        };
    }

    [Serializable]
    public sealed class ReplayEvent
    {
        public long Sequence;
        public int Round;
        public string EventType = string.Empty;
        public string Data = string.Empty;
        public string PreviousHash = string.Empty;
        public string IntegrityHash = string.Empty;
        public ReplayEvent DeepCopy() => new ReplayEvent
        {
            Sequence=Sequence, Round=Round, EventType=EventType, Data=Data,
            PreviousHash=PreviousHash, IntegrityHash=IntegrityHash
        };
    }

    public sealed class ReplayRecorder
    {
        private readonly ReplayRecord record;
        public ReplayRecorder(string matchId, int seed)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("Match ID is required.", nameof(matchId));
            record=new ReplayRecord { MatchId=matchId, Seed=seed, ContainsAuthorityPrivateData=true };
        }
        public void SetScenarioVersion(string scenarioVersion)
        {
            if (string.IsNullOrWhiteSpace(scenarioVersion)) throw new ArgumentException("Scenario version is required.", nameof(scenarioVersion));
            record.ScenarioVersion=scenarioVersion.Trim();
            var previous=ReplayIntegrity.GenesisHash;
            foreach (var item in record.Events)
            {
                item.PreviousHash=previous;
                item.IntegrityHash=ReplayIntegrity.ComputeEventHash(record.SchemaVersion, record.RulesVersion,
                    record.ScenarioVersion, record.MatchId, record.Seed, record.ContainsAuthorityPrivateData, record.SeedRedacted, item);
                previous=item.IntegrityHash;
            }
            record.FinalHash=previous;
        }
        public void Record(int round, string eventType, string data)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Replay event type is required.", nameof(eventType));
            var previous=record.Events.Count == 0 ? ReplayIntegrity.GenesisHash : record.Events[record.Events.Count - 1].IntegrityHash;
            var item=new ReplayEvent
            {
                Sequence=record.Events.Count + 1L, Round=round, EventType=eventType,
                Data=data ?? string.Empty, PreviousHash=previous
            };
            item.IntegrityHash=ReplayIntegrity.ComputeEventHash(record.SchemaVersion, record.RulesVersion,
                record.ScenarioVersion, record.MatchId, record.Seed, record.ContainsAuthorityPrivateData, record.SeedRedacted, item);
            record.Events.Add(item);
            record.FinalHash=item.IntegrityHash;
        }
        /// <summary>Public projection omits ballot and authority payloads and is re-hashed after redaction.</summary>
        public ReplayRecord GetRecord()
        {
            var projection=record.DeepCopy();
            projection.ContainsAuthorityPrivateData=false;
            projection.Seed=0;
            projection.SeedRedacted=true;
            var previous=ReplayIntegrity.GenesisHash;
            foreach (var item in projection.Events)
            {
                item.Data=string.Empty;
                item.PreviousHash=previous;
                item.IntegrityHash=ReplayIntegrity.ComputeEventHash(projection.SchemaVersion, projection.RulesVersion,
                    projection.ScenarioVersion, projection.MatchId, projection.Seed, projection.ContainsAuthorityPrivateData, projection.SeedRedacted, item);
                previous=item.IntegrityHash;
            }
            projection.FinalHash=previous;
            return projection;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Full private replay payload is compiled out of release players.</summary>
        public ReplayRecord GetDeveloperRecord() => record.DeepCopy();
#endif
    }

    public sealed class ReplayValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }
        internal ReplayValidationResult(IEnumerable<string> errors)
        {
            var copy=errors.ToArray(); Errors=Array.AsReadOnly(copy); IsValid=copy.Length == 0;
        }
    }

    /// <summary>Detects accidental replay edits and truncation. Hashes are not signatures or authorization.</summary>
    public static class ReplayIntegrity
    {
        public const string GenesisHash = "GENESIS";

        public static ReplayValidationResult Validate(ReplayRecord record)
        {
            var errors=new List<string>();
            if (record == null) return new ReplayValidationResult(new[] { "Replay is missing." });
            if (record.SchemaVersion != 1) errors.Add("Unsupported replay schema.");
            if (string.IsNullOrWhiteSpace(record.RulesVersion)) errors.Add("Rules version is missing.");
            if (string.IsNullOrWhiteSpace(record.ScenarioVersion)) errors.Add("Scenario version is missing.");
            if (string.IsNullOrWhiteSpace(record.MatchId)) errors.Add("Match identity is missing.");
            if (record.Events == null || record.Events.Count == 0) errors.Add("Replay contains no events.");
            if (record.Events != null)
            {
                var previous=GenesisHash;
                for (var i=0; i<record.Events.Count; i++)
                {
                    var item=record.Events[i];
                    if (item == null) { errors.Add("Replay contains a missing event at index " + i.ToString(CultureInfo.InvariantCulture) + "."); continue; }
                    if (item.Sequence != i + 1L) errors.Add("Replay event sequence is discontinuous at index " + i.ToString(CultureInfo.InvariantCulture) + ".");
                    if (item.Round < 1 || string.IsNullOrWhiteSpace(item.EventType)) errors.Add("Replay event metadata is invalid at index " + i.ToString(CultureInfo.InvariantCulture) + ".");
                    if (!string.Equals(item.PreviousHash, previous, StringComparison.Ordinal)) errors.Add("Replay hash chain is discontinuous at sequence " + item.Sequence.ToString(CultureInfo.InvariantCulture) + ".");
                    var expected=ComputeEventHash(record.SchemaVersion, record.RulesVersion, record.ScenarioVersion,
                        record.MatchId, record.Seed, record.ContainsAuthorityPrivateData, record.SeedRedacted, item);
                    if (!FixedTimeEquals(item.IntegrityHash, expected)) errors.Add("Replay integrity hash mismatch at sequence " + item.Sequence.ToString(CultureInfo.InvariantCulture) + ".");
                    previous=item.IntegrityHash ?? string.Empty;
                }
                if (!FixedTimeEquals(record.FinalHash, previous)) errors.Add("Replay final hash does not match its event history.");
            }
            return new ReplayValidationResult(errors);
        }

        internal static string ComputeEventHash(int schemaVersion, string rulesVersion, string scenarioVersion,
            string matchId, int seed, bool containsAuthorityPrivateData, bool seedRedacted, ReplayEvent item)
        {
            var builder=new StringBuilder();
            Add(builder, schemaVersion.ToString(CultureInfo.InvariantCulture)); Add(builder, rulesVersion);
            Add(builder, scenarioVersion); Add(builder, matchId); Add(builder, seed.ToString(CultureInfo.InvariantCulture));
            Add(builder, containsAuthorityPrivateData ? "private" : "public"); Add(builder, seedRedacted ? "seed-redacted" : "seed-present");
            Add(builder, item.Sequence.ToString(CultureInfo.InvariantCulture)); Add(builder, item.Round.ToString(CultureInfo.InvariantCulture));
            Add(builder, item.EventType); Add(builder, item.Data); Add(builder, item.PreviousHash);
            using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void Add(StringBuilder builder, string value)
        {
            var text=value ?? string.Empty;
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append('|');
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference=0;
            for (var i=0; i<left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }

    [Serializable]
    public sealed class MatchTraceEvent
    {
        public long Sequence;
        public long TimestampUtcTicks;
        public string MatchId = string.Empty;
        public int Round;
        public string Phase = string.Empty;
        public string EventType = string.Empty;
        public string PublicStateHash = string.Empty;
        public MatchTraceEvent DeepCopy() => new MatchTraceEvent
        {
            Sequence=Sequence, TimestampUtcTicks=TimestampUtcTicks, MatchId=MatchId,
            Round=Round, Phase=Phase, EventType=EventType, PublicStateHash=PublicStateHash
        };
    }

    public sealed class MatchTraceRecorder
    {
        private readonly List<MatchTraceEvent> events=new List<MatchTraceEvent>();
        private readonly Func<DateTime> clock;
        public MatchTraceRecorder(Func<DateTime> timestampProvider = null) { clock=timestampProvider ?? (() => DateTime.UtcNow); }
        public void Record(GameRuntimeState state, string eventType)
        {
            if (state == null || state.Match == null || state.Match.CurrentRoundState == null) throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Trace event type is required.", nameof(eventType));
            events.Add(new MatchTraceEvent
            {
                Sequence=events.Count + 1L, TimestampUtcTicks=clock().ToUniversalTime().Ticks,
                MatchId=state.Match.MatchId, Round=state.Match.CurrentRound,
                Phase=state.Match.CurrentRoundState.Phase.ToString(), EventType=eventType,
                PublicStateHash=StateIntegrityHasher.ComputePublicStateHash(state)
            });
        }
        public IReadOnlyList<MatchTraceEvent> GetEvents() => Array.AsReadOnly(events.Select(item => item.DeepCopy()).ToArray());
    }

    /// <summary>Stable digest of public match state only; private roles, clues, ballots, and truth are excluded.</summary>
    public static class StateIntegrityHasher
    {
        public static string ComputePublicStateHash(GameRuntimeState state)
        {
            if (state == null || state.Match == null || state.Match.CurrentRoundState == null || state.Match.Players == null)
                throw new ArgumentNullException(nameof(state));
            var match=state.Match; var round=match.CurrentRoundState; var builder=new StringBuilder();
            Add(builder, match.Seed.ToString(CultureInfo.InvariantCulture)); Add(builder, match.TotalRounds.ToString(CultureInfo.InvariantCulture));
            Add(builder, match.CurrentRound.ToString(CultureInfo.InvariantCulture)); Add(builder, match.Status.ToString());
            // RoundId embeds a random MatchId; omit it so equivalent seeded runs can compare hashes.
            Add(builder, round.ScenarioId); Add(builder, round.Phase.ToString());
            Add(builder, round.VoteRound.ToString(CultureInfo.InvariantCulture)); Add(builder, round.Resolution.ToString());
            Add(builder, round.Completed ? "1" : "0"); Add(builder, round.ScoringResolved ? "1" : "0");
            foreach (var player in match.Players.OrderBy(player => player.PlayerId))
            {
                Add(builder, player.PlayerId.ToString(CultureInfo.InvariantCulture));
                Add(builder, player.MatchScore.ToString(CultureInfo.InvariantCulture));
                Add(builder, player.RoundScore.ToString(CultureInfo.InvariantCulture));
                Add(builder, player.Connected ? "1" : "0");
            }
            using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", string.Empty).ToLowerInvariant();
        }
        private static void Add(StringBuilder builder, string value)
        {
            var text=value ?? string.Empty;
            builder.Append(text.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(text).Append('|');
        }
    }

    public sealed class LocalMatchTestDriver
    {
        private readonly MatchController match;
        public LocalMatchTestDriver(MatchController match) { this.match=match ?? throw new ArgumentNullException(nameof(match)); }
        public VoteResolution PlayRound(int[] voteTargets, int[] runoffTargets = null)
        {
            if (voteTargets == null || voteTargets.Length != match.State.Match.Players.Count) throw new ArgumentException("Invalid vote array.", nameof(voteTargets));
            match.BeginDiscussion(); match.BeginVoting();
            for (var i=0; i<voteTargets.Length; i++) if (!match.SubmitVote(match.State.Match.Players[i].PlayerId, voteTargets[i])) throw new InvalidOperationException("Test driver ballot was rejected.");
            var result=match.ResolveVote();
            if (result == VoteResolution.RevoteRequired)
            {
                if (runoffTargets == null || runoffTargets.Length != voteTargets.Length) throw new InvalidOperationException("A tie requires an explicit runoff ballot.");
                for (var i=0; i<runoffTargets.Length; i++) if (!match.SubmitVote(match.State.Match.Players[i].PlayerId, runoffTargets[i])) throw new InvalidOperationException("Test driver runoff ballot was rejected.");
                result=match.ResolveVote();
            }
            match.CompleteReveal(); match.ResolveScoring(); match.FinishRound();
            return result;
        }
    }
}
