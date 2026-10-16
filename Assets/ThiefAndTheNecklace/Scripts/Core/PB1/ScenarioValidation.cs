using System;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public interface IScenarioGenerator { ScenarioTruthModel Generate(int seed, int playerCount); }

    public sealed class StaticScenarioGenerator : IScenarioGenerator
    {
        private readonly ScenarioTruthModel template;
        public StaticScenarioGenerator(ScenarioTruthModel template) { this.template = (template ?? throw new ArgumentNullException(nameof(template))).DeepCopy(); }
        public ScenarioTruthModel Generate(int seed, int playerCount)
        {
            if (playerCount < 4 || playerCount > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));
            return template.DeepCopy();
        }
    }

    public sealed class ScenarioValidator
    {
        public ScenarioValidationResult Validate(ScenarioTruthModel scenario, int playerCount)
        {
            var errors = new System.Collections.Generic.List<string>();
            if (scenario == null) return new ScenarioValidationResult { Valid = false, Errors = new[] { "Scenario is null." } };
            if (playerCount < 4 || playerCount > 6) errors.Add("Player count must be between 4 and 6.");
            if (string.IsNullOrWhiteSpace(scenario.ScenarioId)) errors.Add("Scenario ID is required.");
            if (scenario.Timeline == null || scenario.Timeline.Count == 0) errors.Add("Timeline is empty.");
            else if (!scenario.Timeline.Any(e => e != null && e.Critical)) errors.Add("No critical timeline events are defined.");
            if (scenario.Facts == null || scenario.Facts.Count == 0) errors.Add("No facts defined.");
            if (scenario.Evidence == null || scenario.Evidence.Count == 0) errors.Add("No evidence defined.");
            if (scenario.Opportunities == null || scenario.Opportunities.Count == 0) errors.Add("No opportunity windows defined.");
            if (scenario.PlayerInformation == null || scenario.PlayerInformation.Count != playerCount) errors.Add("Player information count does not match player count.");

            var timeline = scenario.Timeline ?? new System.Collections.Generic.List<TimelineEvent>();
            var opportunities = scenario.Opportunities ?? new System.Collections.Generic.List<OpportunityWindow>();
            if (timeline.Any(e => e == null || string.IsNullOrWhiteSpace(e.EventId) || string.IsNullOrWhiteSpace(e.LocationId) || string.IsNullOrWhiteSpace(e.Action) || float.IsNaN(e.StartTime) || float.IsInfinity(e.StartTime) || float.IsNaN(e.EndTime) || float.IsInfinity(e.EndTime) || e.EndTime < e.StartTime)) errors.Add("Timeline contains incomplete events or invalid time windows.");
            if (timeline.Where(e => e != null).Select(e => e.EventId).Distinct(StringComparer.Ordinal).Count() != timeline.Count) errors.Add("Timeline event IDs must be unique.");
            if (!timeline.Where(e => e != null).OrderBy(e => e.StartTime).ThenBy(e => e.Sequence).SequenceEqual(timeline.Where(e => e != null))) errors.Add("Timeline is not ordered chronologically.");
            if (timeline.Where(e => e != null).Select(e => e.Sequence).Distinct().Count() != timeline.Count(e => e != null)) errors.Add("Timeline sequence numbers must be unique.");

            var facts = scenario.Facts ?? new System.Collections.Generic.List<FactDefinition>();
            if (facts.Any(f => f == null || string.IsNullOrWhiteSpace(f.FactId) || string.IsNullOrWhiteSpace(f.SubjectId) || string.IsNullOrWhiteSpace(f.Predicate) || string.IsNullOrWhiteSpace(f.ObjectId) || !Enum.IsDefined(typeof(EvidenceTruth), f.Truth))) errors.Add("Facts contain incomplete or invalid definitions.");
            if (facts.Where(f => f != null).Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() != facts.Count) errors.Add("Fact IDs must be unique.");
            if (facts.Where(f => f != null).Select(f => new FactKey(f.SubjectId, f.Predicate, f.ObjectId)).Distinct().Count() != facts.Count(f => f != null)) errors.Add("Duplicate logical facts are not allowed.");
            var factIds = new System.Collections.Generic.HashSet<string>(facts.Where(f => f != null).Select(f => f.FactId), StringComparer.Ordinal);
            if (new ContradictionService().FindContradictions(facts).ContradictionFound) errors.Add("World truth contains contradictory facts.");

            var evidence = scenario.Evidence ?? new System.Collections.Generic.List<EvidenceDefinition>();
            if (evidence.Any(e => e == null || string.IsNullOrWhiteSpace(e.EvidenceId) || string.IsNullOrWhiteSpace(e.Description))) errors.Add("Evidence contains incomplete definitions.");
            if (evidence.Where(e => e != null).Select(e => e.EvidenceId).Distinct(StringComparer.Ordinal).Count() != evidence.Count) errors.Add("Evidence IDs must be unique.");
            var eventIds = new System.Collections.Generic.HashSet<string>(timeline.Where(e => e != null).Select(e => e.EventId), StringComparer.Ordinal);
            var evidenceIds = new System.Collections.Generic.HashSet<string>(evidence.Where(e => e != null).Select(e => e.EvidenceId), StringComparer.Ordinal);
            foreach (var item in evidence.Where(e => e != null))
            {
                if ((item.SupportsFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id)) || (item.ContradictsFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id))) errors.Add("Evidence " + item.EvidenceId + " references an unknown fact.");
                if ((item.RequiredEventIds ?? new System.Collections.Generic.List<string>()).Any(id => !eventIds.Contains(id))) errors.Add("Evidence " + item.EvidenceId + " references an unknown timeline event.");
            }
            if (!HasCriticalEventCoverage(scenario)) errors.Add("Each critical event needs at least two independent evidence paths.");
            if (!opportunities.Any(o => o != null && !float.IsNaN(o.StartTime) && !float.IsInfinity(o.StartTime) && !float.IsNaN(o.EndTime) && !float.IsInfinity(o.EndTime) && o.EndTime >= o.StartTime && !string.IsNullOrWhiteSpace(o.PlayerId) && !string.IsNullOrWhiteSpace(o.LocationId) && o.HasAccess && o.PhysicallyPossible)) errors.Add("No physically possible access opportunity exists.");
            if (scenario.PlayerInformation != null)
            {
                if (scenario.PlayerInformation.Where(p => p != null).Select(p => p.PlayerId).Distinct(StringComparer.Ordinal).Count() != scenario.PlayerInformation.Count(p => p != null)) errors.Add("Player information IDs must be unique.");
                if (scenario.PlayerInformation.Where(p => p != null).Select(p => p.Role).Distinct().Count() != scenario.PlayerInformation.Count(p => p != null)) errors.Add("Player information roles must be unique.");
                foreach (var player in scenario.PlayerInformation.Where(p => p != null))
                {
                    if (string.IsNullOrWhiteSpace(player.PlayerId) || string.IsNullOrWhiteSpace(player.PrivateSituation) || string.IsNullOrWhiteSpace(player.Objective) || player.EvidenceIds == null || player.EvidenceIds.Count == 0) errors.Add("Every player information bundle needs an ID, situation, objective and private evidence.");
                    if ((player.EvidenceIds ?? new System.Collections.Generic.List<string>()).Any(id => !evidenceIds.Contains(id))) errors.Add("Player information references unknown evidence.");
                    if ((player.KnownFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id)) || (player.UnknownFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id)) || (player.FalseFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id))) errors.Add("Player information references unknown facts.");
                    var knownStates = (player.KnownFactIds ?? new System.Collections.Generic.List<string>()).Concat(player.UnknownFactIds ?? new System.Collections.Generic.List<string>()).Concat(player.FalseFactIds ?? new System.Collections.Generic.List<string>()).ToList();
                    if (knownStates.Distinct(StringComparer.Ordinal).Count() != knownStates.Count) errors.Add("A player fact cannot be assigned more than one knowledge state.");
                }
            }
            return new ScenarioValidationResult { Valid = errors.Count == 0, Errors = errors.Distinct().ToArray() };
        }

        public bool HasCriticalEventCoverage(ScenarioTruthModel scenario)
        {
            if (scenario == null || scenario.Timeline == null || scenario.Evidence == null) return false;
            foreach (var ev in scenario.Timeline.Where(e => e != null && e.Critical))
                if (scenario.Evidence.Count(e => e != null && e.Independent && e.RequiredEventIds != null && e.RequiredEventIds.Contains(ev.EventId)) < 2) return false;
            return true;
        }
    }
}
