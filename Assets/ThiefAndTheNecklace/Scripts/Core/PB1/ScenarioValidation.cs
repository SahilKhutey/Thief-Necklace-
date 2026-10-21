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
        public ScenarioValidationResult Validate(ScenarioTruthModel scenario, int playerCount, bool requirePlayerPackages = true)
        {
            var errors = new System.Collections.Generic.List<string>();
            if (scenario == null) return new ScenarioValidationResult { Valid = false, Errors = new[] { "Scenario is null." } };
            if (playerCount < 4 || playerCount > 6) errors.Add("Player count must be between 4 and 6.");
            if (string.IsNullOrWhiteSpace(scenario.ScenarioId)) errors.Add("Scenario ID is required.");
            if (string.IsNullOrWhiteSpace(scenario.ScenarioVersion) || string.IsNullOrWhiteSpace(scenario.GeneratorVersion)) errors.Add("Scenario and generator versions are required.");
            if (scenario.Timeline == null || scenario.Timeline.Count == 0) errors.Add("Timeline is empty.");
            else if (!scenario.Timeline.Any(e => e != null && e.Critical)) errors.Add("No critical timeline events are defined.");
            if (scenario.Facts == null || scenario.Facts.Count == 0) errors.Add("No facts defined.");
            if (scenario.Evidence == null || scenario.Evidence.Count == 0) errors.Add("No evidence defined.");
            if (scenario.Opportunities == null || scenario.Opportunities.Count == 0) errors.Add("No opportunity windows defined.");
            if (scenario.PlayerInformation == null || (requirePlayerPackages && scenario.PlayerInformation.Count != playerCount)) errors.Add("Player information count does not match player count.");

            var timeline = scenario.Timeline ?? new System.Collections.Generic.List<TimelineEvent>();
            var opportunities = scenario.Opportunities ?? new System.Collections.Generic.List<OpportunityWindow>();
            if (timeline.Any(e => e == null || string.IsNullOrWhiteSpace(e.EventId) || string.IsNullOrWhiteSpace(e.LocationId) || string.IsNullOrWhiteSpace(e.Action) || float.IsNaN(e.StartTime) || float.IsInfinity(e.StartTime) || float.IsNaN(e.EndTime) || float.IsInfinity(e.EndTime) || e.EndTime < e.StartTime)) errors.Add("Timeline contains incomplete events or invalid time windows.");
            if (timeline.Where(e => e != null).Select(e => e.EventId).Distinct(StringComparer.Ordinal).Count() != timeline.Count) errors.Add("Timeline event IDs must be unique.");
            if (!timeline.Where(e => e != null).OrderBy(e => e.StartTime).ThenBy(e => e.Sequence).SequenceEqual(timeline.Where(e => e != null))) errors.Add("Timeline is not ordered chronologically.");
            if (timeline.Where(e => e != null).Select(e => e.Sequence).Distinct().Count() != timeline.Count(e => e != null)) errors.Add("Timeline sequence numbers must be unique.");
            var theftEvents=timeline.Where(e => e != null && e.IsTheftEvent).ToList();
            if (theftEvents.Count != 1) errors.Add("Scenario must contain exactly one explicit theft event.");
            var eventsById = timeline.Where(e => e != null).GroupBy(e => e.EventId, StringComparer.Ordinal).Where(group => !string.IsNullOrEmpty(group.Key)).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (var ev in timeline.Where(e => e != null))
            {
                if ((ev.CauseEventIds ?? new System.Collections.Generic.List<string>()).Any(id => !eventsById.ContainsKey(id))) errors.Add("Event " + ev.EventId + " references an unknown cause.");
                foreach (var causeId in ev.CauseEventIds ?? new System.Collections.Generic.List<string>())
                    if (eventsById.TryGetValue(causeId, out var cause) && (cause.StartTime > ev.StartTime || cause.EventId == ev.EventId)) errors.Add("Event causal order is invalid for " + ev.EventId + ".");
                if ((ev.RelatedFactIds ?? new System.Collections.Generic.List<string>()).Any(string.IsNullOrWhiteSpace)) errors.Add("Event " + ev.EventId + " contains an empty fact reference.");
            }
            if (HasCausalCycle(eventsById)) errors.Add("Timeline causal graph contains a cycle.");
            if (timeline.Where(e => e != null && e.ActorId != "Palace").GroupBy(e => e.ActorId, StringComparer.Ordinal).Any(group => group.Where(e => e.ActorId != "Environment").SelectMany(e => group.Where(other => !ReferenceEquals(e, other) && e.LocationId != other.LocationId && e.StartTime <= other.EndTime && other.StartTime <= e.EndTime)).Any()))
                errors.Add("An actor is assigned simultaneous events in different locations.");

            var locationIds = (scenario.Locations ?? new System.Collections.Generic.List<LocationState>()).Where(l => l != null).Select(l => l.LocationId).ToList();
            if (locationIds.Count > 0)
            {
                if (locationIds.Any(string.IsNullOrWhiteSpace) || locationIds.Distinct(StringComparer.Ordinal).Count() != locationIds.Count) errors.Add("Location IDs must be present and unique.");
                var locationSet = new System.Collections.Generic.HashSet<string>(locationIds, StringComparer.Ordinal);
                foreach (var location in scenario.Locations.Where(l => l != null))
                    if (string.IsNullOrWhiteSpace(location.DisplayName) || (location.ConnectedLocationIds ?? new System.Collections.Generic.List<string>()).Any(id => !locationSet.Contains(id))) errors.Add("Location " + location.LocationId + " has incomplete metadata or invalid connections.");
                if (timeline.Where(e => e != null).Any(e => !locationSet.Contains(e.LocationId))) errors.Add("Timeline references an unknown location.");
                var graph=scenario.Locations.Where(l => l != null && !string.IsNullOrEmpty(l.LocationId)).GroupBy(l => l.LocationId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().ConnectedLocationIds ?? new System.Collections.Generic.List<string>(), StringComparer.Ordinal);
                var environmentActors=new System.Collections.Generic.HashSet<string>((scenario.Actors ?? new System.Collections.Generic.List<ActorState>()).Where(a => a != null && a.Type == ScenarioActorType.Environment).Select(a => a.ActorId), StringComparer.Ordinal);
                foreach (var actorEvents in timeline.Where(e => e != null && !environmentActors.Contains(e.ActorId)).GroupBy(e => e.ActorId, StringComparer.Ordinal))
                {
                    var ordered=actorEvents.OrderBy(e => e.StartTime).ThenBy(e => e.Sequence).ToList();
                    for (var i=1; i<ordered.Count; i++)
                        if (ordered[i-1].LocationId != ordered[i].LocationId && !LocationsConnected(graph, ordered[i-1].LocationId, ordered[i].LocationId)) errors.Add("Actor " + actorEvents.Key + " moves between disconnected locations.");
                }
            }
            var actorIds = (scenario.Actors ?? new System.Collections.Generic.List<ActorState>()).Where(a => a != null).Select(a => a.ActorId).ToList();
            if (actorIds.Count > 0)
            {
                if (actorIds.Any(string.IsNullOrWhiteSpace) || actorIds.Distinct(StringComparer.Ordinal).Count() != actorIds.Count) errors.Add("Actor IDs must be present and unique.");
                var actorSet = new System.Collections.Generic.HashSet<string>(actorIds, StringComparer.Ordinal);
                if (timeline.Where(e => e != null).Any(e => !actorSet.Contains(e.ActorId))) errors.Add("Timeline references an unknown actor.");
                foreach (var actor in scenario.Actors.Where(a => a != null))
                    if ((actor.AccessibleLocationIds ?? new System.Collections.Generic.List<string>()).Any(id => !locationIds.Contains(id, StringComparer.Ordinal))) errors.Add("Actor " + actor.ActorId + " references an unknown accessible location.");
            }
            var objectIds = (scenario.Objects ?? new System.Collections.Generic.List<ScenarioObjectState>()).Where(o => o != null).Select(o => o.ObjectId).ToList();
            if (objectIds.Count > 0 && (objectIds.Any(string.IsNullOrWhiteSpace) || objectIds.Distinct(StringComparer.Ordinal).Count() != objectIds.Count || scenario.Objects.Where(o => o != null).Any(o => locationIds.Count > 0 && !locationIds.Contains(o.LocationId, StringComparer.Ordinal)))) errors.Add("Scenario objects must have unique identities and valid locations.");
            if (objectIds.Count > 0 && !objectIds.Contains(scenario.StolenObjectId, StringComparer.Ordinal)) errors.Add("The declared stolen object is missing from scenario state.");
            var objectTransitions=scenario.ObjectTransitions ?? new System.Collections.Generic.List<ObjectTransition>();
            if (objectTransitions.Any(t => t == null || !objectIds.Contains(t.ObjectId, StringComparer.Ordinal) || !timeline.Any(ev => ev != null && ev.EventId == t.EventId) || (!string.IsNullOrEmpty(t.ToActorId) && actorIds.Count > 0 && !actorIds.Contains(t.ToActorId, StringComparer.Ordinal)) || (locationIds.Count > 0 && (!locationIds.Contains(t.FromLocationId, StringComparer.Ordinal) || (!string.IsNullOrEmpty(t.ToLocationId) && !locationIds.Contains(t.ToLocationId, StringComparer.Ordinal)))))) errors.Add("Object transitions must reference valid objects, events, and locations.");
            if (theftEvents.Count == 1 && !string.IsNullOrWhiteSpace(scenario.ThiefPlayerId) && !string.Equals(theftEvents[0].ActorId, scenario.ThiefPlayerId, StringComparison.Ordinal)) errors.Add("The theft event actor does not match the assigned Thief identity.");

            var facts = scenario.Facts ?? new System.Collections.Generic.List<FactDefinition>();
            if (facts.Any(f => f == null || string.IsNullOrWhiteSpace(f.FactId) || string.IsNullOrWhiteSpace(f.SubjectId) || string.IsNullOrWhiteSpace(f.Predicate) || string.IsNullOrWhiteSpace(f.ObjectId) || !Enum.IsDefined(typeof(EvidenceTruth), f.Truth))) errors.Add("Facts contain incomplete or invalid definitions.");
            if (facts.Where(f => f != null).Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() != facts.Count) errors.Add("Fact IDs must be unique.");
            if (facts.Where(f => f != null).Select(f => new FactKey(f.SubjectId, f.Predicate, f.ObjectId)).Distinct().Count() != facts.Count(f => f != null)) errors.Add("Duplicate logical facts are not allowed.");
            var factIds = new System.Collections.Generic.HashSet<string>(facts.Where(f => f != null).Select(f => f.FactId), StringComparer.Ordinal);
            foreach (var ev in timeline.Where(e => e != null))
                if ((ev.RelatedFactIds ?? new System.Collections.Generic.List<string>()).Any(id => !factIds.Contains(id))) errors.Add("Event " + ev.EventId + " references an unknown fact.");
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
                if (!string.IsNullOrEmpty(item.SourceEventId) && !eventIds.Contains(item.SourceEventId)) errors.Add("Evidence " + item.EvidenceId + " has an unknown source event.");
                if (item.IsFalseLead && (!(item.SupportsFactIds ?? new System.Collections.Generic.List<string>()).Any(id => facts.Any(f => f != null && f.FactId == id && f.Truth == EvidenceTruth.False)) || (item.RequiredEventIds ?? new System.Collections.Generic.List<string>()).Count == 0)) errors.Add("False lead " + item.EvidenceId + " must originate from a traceable world observation supporting an authored false proposition.");
            }
            var opportunityIds=new System.Collections.Generic.HashSet<string>(opportunities.Where(o => o != null).Select(o => o.PlayerId), StringComparer.Ordinal);
            foreach (var item in evidence.Where(e => e != null))
                if ((item.EliminatesPlayerIds ?? new System.Collections.Generic.List<string>()).Any(id => !opportunityIds.Contains(id) || id == scenario.ThiefPlayerId)) errors.Add("Evidence " + item.EvidenceId + " eliminates an invalid suspect candidate.");
            if (!HasCriticalEventCoverage(scenario)) errors.Add("Each critical event needs at least two independent evidence paths.");
            foreach (var criticalFact in (scenario.CriticalFactIds ?? new System.Collections.Generic.List<string>()).Concat(facts.Where(f => f != null && f.Critical).Select(f => f.FactId)).Distinct(StringComparer.Ordinal))
                if (!factIds.Contains(criticalFact) || new EvidenceGraphValidator().CountIndependentPaths(scenario, criticalFact) < 2) errors.Add("Critical fact " + criticalFact + " needs two independent evidence paths.");
            if (!opportunities.Any(o => o != null && !float.IsNaN(o.StartTime) && !float.IsInfinity(o.StartTime) && !float.IsNaN(o.EndTime) && !float.IsInfinity(o.EndTime) && o.EndTime >= o.StartTime && !string.IsNullOrWhiteSpace(o.PlayerId) && !string.IsNullOrWhiteSpace(o.LocationId) && o.HasAccess && o.PhysicallyPossible)) errors.Add("No physically possible access opportunity exists.");
            if (theftEvents.Count == 1 && !opportunities.Any(o => o != null && o.PlayerId == scenario.ThiefPlayerId && new OpportunityService().HasOpportunity(o, theftEvents[0].StartTime, theftEvents[0].LocationId))) errors.Add("The assigned Thief has no complete opportunity for the theft event.");
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
                    var criticalFacts=(scenario.CriticalFactIds ?? new System.Collections.Generic.List<string>()).Concat(facts.Where(f => f != null && f.Critical).Select(f => f.FactId)).Distinct(StringComparer.Ordinal).ToList();
                    if (player.PlayerId != scenario.ThiefPlayerId && criticalFacts.Count > 0 && criticalFacts.All(id => (player.KnownFactIds ?? new System.Collections.Generic.List<string>()).Contains(id))) errors.Add("An innocent player receives the complete set of critical solution facts.");
                }
            }
            return new ScenarioValidationResult { Valid = errors.Count == 0, Errors = errors.Distinct().ToArray() };
        }

        public bool HasCriticalEventCoverage(ScenarioTruthModel scenario)
        {
            if (scenario == null || scenario.Timeline == null || scenario.Evidence == null) return false;
            foreach (var ev in scenario.Timeline.Where(e => e != null && e.Critical))
                if (scenario.Evidence.Where(e => e != null && e.RequiredEventIds != null && e.RequiredEventIds.Contains(ev.EventId)).Select(e => !string.IsNullOrWhiteSpace(e.CausalPathId) ? e.CausalPathId : e.SourceId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Count() < 2) return false;
            return true;
        }

        private static bool HasCausalCycle(System.Collections.Generic.Dictionary<string, TimelineEvent> events)
        {
            var visiting = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var visited = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> visit = null;
            visit = id =>
            {
                if (visiting.Contains(id)) return true;
                if (!visited.Add(id)) return false;
                visiting.Add(id);
                foreach (var cause in events[id].CauseEventIds ?? new System.Collections.Generic.List<string>())
                    if (events.ContainsKey(cause) && visit(cause)) return true;
                visiting.Remove(id);
                return false;
            };
            return events.Keys.Any(visit);
        }

        private static bool LocationsConnected(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> graph, string from, string to)
        {
            if (!graph.ContainsKey(from) || !graph.ContainsKey(to)) return false;
            var pending=new System.Collections.Generic.Queue<string>();
            var visited=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal) { from };
            pending.Enqueue(from);
            while (pending.Count > 0)
            {
                var current=pending.Dequeue();
                foreach (var next in graph[current])
                {
                    if (next == to) return true;
                    if (visited.Add(next)) pending.Enqueue(next);
                }
            }
            return from == to;
        }
    }
}
