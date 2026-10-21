using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public static class DefaultScenarioFactory
    {
        public static ScenarioDefinitionData CreateRoyalNecklace()
        {
            var scenario = new ScenarioDefinitionData
            {
                ScenarioId = "royal-necklace",
                Title = "The Royal Necklace",
                Introduction = "The King's necklace vanished during a palace blackout.",
                HasThiefOverride = true,
                Events = new List<ScenarioEvent>
                {
                    new ScenarioEvent { EventId = "banquet", Time = 0f, Location = "Grand Hall", Description = "21:00 — the banquet begins." },
                    new ScenarioEvent { EventId = "blackout", Time = 10f, Location = "Palace", Description = "21:10 — the palace lights go out." },
                    new ScenarioEvent { EventId = "theft", Time = 14f, Location = "Royal Chamber", Description = "21:14 — the Necklace is taken." },
                    new ScenarioEvent { EventId = "discovery", Time = 20f, Location = "Grand Hall", Description = "21:20 — the Necklace is found missing." }
                }
            };
            scenario.PlayerInformation.Add(Info(RoleType.King, "You spoke with the Queen shortly before she left the banquet.", "The Queen left before the blackout."));
            scenario.PlayerInformation.Add(Info(RoleType.Queen, "You left the banquet before the blackout.", "You heard a door open near the Royal Chamber."));
            scenario.PlayerInformation.Add(Info(RoleType.Minister, "You checked documents near the Treasury.", "The Treasury corridor was quiet during the blackout."));
            scenario.PlayerInformation.Add(Info(RoleType.Soldier, "You guarded the western gate.", "The western gate stayed closed throughout the blackout."));
            scenario.PlayerInformation.Add(Info(RoleType.Guard, "You were called away from the east corridor.", "You noticed fresh marks near the Royal Chamber."));
            scenario.PlayerInformation.Add(Info(RoleType.Servant, "You passed through the East Corridor before the blackout.", "You heard footsteps near the Royal Chamber."));
            scenario.ThiefSituation = "During the blackout, you took the Necklace and escaped through the East Corridor. Keep your identity hidden.";
            scenario.ThiefClues.Add("You know the blackout lasted long enough to reach the display and leave.");
            scenario.TruthModel = CreateRoyalNecklaceTruth();
            return scenario;
        }

        public static ScenarioTruthModel CreateRoyalNecklaceTruth()
        {
            var truth = new ScenarioTruthModel { ScenarioId = "royal-necklace", PublicStoryContext="The King's necklace vanished during a palace blackout." };
            truth.Locations.AddRange(new[] { Location("Grand Hall", true, "East Corridor", "Garden"), Location("Royal Chamber", false, "East Corridor"), Location("Treasury", false, "East Corridor"), Location("Garden", true, "Grand Hall"), Location("East Corridor", false, "Royal Chamber", "Treasury", "Grand Hall"), Location("West Gate", true, "West Passage"), Location("West Passage", false, "West Gate", "Grand Hall"), Location("Palace", true) });
            truth.Actors.Add(new ActorState { ActorId="Thief", Type=ScenarioActorType.Player, DisplayName="Hidden Thief", AccessibleLocationIds=new List<string> { "Grand Hall", "East Corridor", "Royal Chamber", "Garden" }, AvailableActions=new List<string> { "move", "unlock", "take" } });
            truth.Actors.Add(new ActorState { ActorId="Minister", Type=ScenarioActorType.Player, DisplayName="Minister", AccessibleLocationIds=new List<string> { "Grand Hall", "East Corridor", "Royal Chamber", "Treasury" }, AvailableActions=new List<string> { "move", "unlock" } });
            truth.Actors.Add(new ActorState { ActorId="Candidate", Type=ScenarioActorType.Player, DisplayName="A guest", AccessibleLocationIds=new List<string> { "Grand Hall", "East Corridor", "Royal Chamber" }, AvailableActions=new List<string> { "move", "unlock" } });
            foreach (var role in new[] { "King", "Queen", "Soldier", "Guard", "Servant" }) truth.Actors.Add(new ActorState { ActorId=role, Type=ScenarioActorType.Player, DisplayName=role, AccessibleLocationIds=truth.Locations.Select(l => l.LocationId).Where(id => id != "Treasury").ToList(), AvailableActions=new List<string> { "move", "observe" } });
            truth.Actors.Add(new ActorState { ActorId="Palace", Type=ScenarioActorType.Environment, DisplayName="Palace", AccessibleLocationIds=truth.Locations.Select(l => l.LocationId).ToList(), AvailableActions=new List<string> { "blackout", "alarm" } });
            truth.Objects.Add(new ScenarioObjectState { ObjectId="Necklace", Type=ScenarioObjectType.Necklace, LocationId="Royal Chamber", IsPublic=true, StateTags=new List<string> { "displayed", "valuable" } });
            truth.Objects.Add(new ScenarioObjectState { ObjectId="TreasuryDoor", Type=ScenarioObjectType.Door, LocationId="Treasury", StateTags=new List<string> { "locked" } });
            truth.Objects.Add(new ScenarioObjectState { ObjectId="DisplayCase", Type=ScenarioObjectType.DisplayCase, LocationId="Royal Chamber", StateTags=new List<string> { "closed", "alarmed" } });
            string[] ids = { "banquet", "queen-leaves", "minister-corridor", "servant-passage", "soldier-gate", "blackout", "thief-enters", "candidate-alibi", "necklace-stolen", "thief-exits", "lights-return", "discovery" };
            string[] actions = { "Banquet begins", "Queen leaves the banquet hall", "Minister enters the eastern corridor", "Servant crosses the west passage", "Soldier guards the western gate", "Palace lights go out", "Unknown person enters the Royal Chamber", "A guest remains in the East Corridor", "Necklace is removed", "Person exits the chamber", "Lights return", "Necklace is discovered missing" };
            string[] locations = { "Grand Hall", "Grand Hall", "East Corridor", "West Passage", "West Gate", "Palace", "Royal Chamber", "East Corridor", "Royal Chamber", "Royal Chamber", "Palace", "Grand Hall" };
            float[] times = { 0, 2, 7, 8, 9, 10, 12, 13, 15, 16, 18, 20 };
            var eventActors = new[] { "Palace", "Queen", "Minister", "Servant", "Soldier", "Palace", "Thief", "Candidate", "Thief", "Thief", "Palace", "Palace" };
            for (var i = 0; i < ids.Length; i++) truth.Timeline.Add(new TimelineEvent { EventId=ids[i], Sequence=i+1, StartTime=times[i], EndTime=ids[i] == "candidate-alibi" ? 16f : times[i], LocationId=locations[i], ActorId=eventActors[i], Action=actions[i], Critical=ids[i] == "thief-enters" || ids[i] == "necklace-stolen" || ids[i] == "thief-exits", IsResolved=true, IsTheftEvent=ids[i] == "necklace-stolen" });
            truth.Timeline.Single(e => e.EventId == "necklace-stolen").CauseEventIds.Add("thief-enters");
            truth.Timeline.Single(e => e.EventId == "thief-exits").CauseEventIds.Add("necklace-stolen");
            truth.Timeline.Single(e => e.EventId == "discovery").CauseEventIds.Add("necklace-stolen");
            truth.ObjectTransitions.Add(new ObjectTransition { ObjectId="Necklace", EventId="necklace-stolen", FromLocationId="Royal Chamber", ToActorId="Thief" });

            truth.Facts.Add(Fact("F_QUEEN_LEFT", "Queen", "left", "Grand Hall", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_MINISTER_EAST", "Minister", "wasAt", "East Corridor", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_CANDIDATE_ALIBI", "Candidate", "occupiedDuring", "East Corridor:13-16", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_SERVANT_WEST", "Servant", "wasAt", "West Passage", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_SOLDIER_GATE", "Soldier", "guarded", "West Gate", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_THIEF_ENTERED", "Thief", "entered", "Royal Chamber", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_NECKLACE_STOLEN", "Thief", "stole", "Necklace", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_LIGHTS_OUT", "Palace", "lights", "out", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_THIEF_EXITED", "Thief", "exited", "Royal Chamber", EvidenceTruth.True));
            truth.Facts.Add(Fact("F_FALSE_SERVANT_TREASURY", "Servant", "wasAt", "Treasury", EvidenceTruth.False));
            truth.Facts.Add(Fact("F_FALSE_GUARD_GARDEN", "Guard", "wasAt", "Garden", EvidenceTruth.False));
            truth.CriticalFactIds.AddRange(new[] { "F_THIEF_ENTERED", "F_NECKLACE_STOLEN", "F_THIEF_EXITED" });

            truth.Opportunities.Add(new OpportunityWindow { PlayerId="Thief", StartTime=10, EndTime=16, LocationId="Royal Chamber", HasAccess=true, PhysicallyPossible=true });
            truth.Opportunities.Add(new OpportunityWindow { PlayerId="Minister", StartTime=10, EndTime=16, LocationId="Royal Chamber", HasAccess=true, PhysicallyPossible=true, HadTime=true, HasCapability=true });
            truth.Evidence.Add(Evidence("E_KING_QUEEN", "The King saw the Queen leave before the blackout.", "F_QUEEN_LEFT", "queen-leaves", true));
            truth.Evidence.Add(Evidence("E_QUEEN_SELF", "The Queen confirms she left the banquet.", "F_QUEEN_LEFT", "queen-leaves", true));
            truth.Evidence.Add(Evidence("E_MINISTER", "The Minister crossed the eastern corridor.", "F_MINISTER_EAST", "minister-corridor", true));
            var candidateAlibiEvidence=Evidence("E_CANDIDATE_ALIBI", "A witness places {player} in the East Corridor throughout 21:13–21:16.", "F_CANDIDATE_ALIBI", "candidate-alibi", true);
            candidateAlibiEvidence.EliminatesPlayerIds.Add("Candidate");
            candidateAlibiEvidence.ClueTargetPlayerId="Candidate";
            truth.Evidence.Add(candidateAlibiEvidence);
            truth.Evidence.Add(Evidence("E_SERVANT", "The Servant crossed the west passage.", "F_SERVANT_WEST", "servant-passage", true));
            truth.Evidence.Add(Evidence("E_SOLDIER_GATE", "The Soldier remained at the western gate.", "F_SOLDIER_GATE", "soldier-gate", true));
            truth.Evidence.Add(Evidence("E_DOOR", "A door opened near the Royal Chamber during the blackout.", "F_THIEF_ENTERED", "thief-enters", true));
            truth.Evidence.Add(Evidence("E_FOOTSTEPS", "Footsteps were heard inside the Royal Chamber.", "F_THIEF_ENTERED", "thief-enters", true));
            truth.Evidence.Add(Evidence("E_DISPLAY", "The display was disturbed during the blackout.", "F_NECKLACE_STOLEN", "necklace-stolen", true));
            truth.Evidence.Add(Evidence("E_CASE", "The protective case was opened from inside the Royal Chamber.", "F_NECKLACE_STOLEN", "necklace-stolen", true));
            truth.Evidence.Add(Evidence("E_GUARD_MARKS", "Fresh marks were found at the chamber exit.", "F_THIEF_EXITED", "thief-exits", true));
            truth.Evidence.Add(Evidence("E_CORRIDOR_TRACE", "A trace leads from the chamber toward the East Corridor.", "F_THIEF_EXITED", "thief-exits", true));
            truth.Evidence.Add(new EvidenceDefinition { EvidenceId="E_AMBIGUOUS_FOOTSTEPS", Description="Footsteps were heard near the chamber; their source was unclear.", SupportsFactIds=new List<string> { "F_THIEF_ENTERED" }, RequiredEventIds=new List<string> { "thief-enters" }, IsAmbiguous=true });
            truth.Evidence.Add(new EvidenceDefinition { EvidenceId="E_AMBIGUOUS_DOOR", Description="A door moved during the blackout, but the direction was uncertain.", SupportsFactIds=new List<string> { "F_THIEF_ENTERED" }, RequiredEventIds=new List<string> { "thief-enters" }, IsAmbiguous=true });
            truth.Evidence.Add(new EvidenceDefinition { EvidenceId="E_FALSE_SERVANT_TREASURY", Description="A faint trace seems to place the Servant near the Treasury.", SupportsFactIds=new List<string> { "F_FALSE_SERVANT_TREASURY" }, RequiredEventIds=new List<string> { "servant-passage" }, IsFalseLead=true });
            truth.Evidence.Add(new EvidenceDefinition { EvidenceId="E_FALSE_GUARD_GARDEN", Description="A partial witness report suggests the Guard crossed the Garden.", SupportsFactIds=new List<string> { "F_FALSE_GUARD_GARDEN" }, RequiredEventIds=new List<string> { "soldier-gate" }, IsFalseLead=true });

            foreach (var evidence in truth.Evidence)
                foreach (var eventId in evidence.RequiredEventIds ?? new List<string>())
                    foreach (var factId in evidence.SupportsFactIds ?? new List<string>())
                        if (!truth.Timeline.Single(e => e.EventId == eventId).RelatedFactIds.Contains(factId)) truth.Timeline.Single(e => e.EventId == eventId).RelatedFactIds.Add(factId);

            AddBundle(truth, RoleType.King, "You remained in the Grand Hall and saw the Queen leave.", "E_KING_QUEEN|E_MINISTER|E_DOOR", "F_QUEEN_LEFT|F_MINISTER_EAST|F_THIEF_ENTERED");
            AddBundle(truth, RoleType.Queen, "You left the banquet before the blackout.", "E_QUEEN_SELF|E_FOOTSTEPS|E_DISPLAY|E_CANDIDATE_ALIBI", "F_QUEEN_LEFT|F_THIEF_ENTERED|F_NECKLACE_STOLEN");
            AddBundle(truth, RoleType.Minister, "You crossed the eastern corridor before the blackout.", "E_MINISTER|E_CASE|E_CORRIDOR_TRACE", "F_MINISTER_EAST|F_NECKLACE_STOLEN|F_THIEF_EXITED");
            AddBundle(truth, RoleType.Soldier, "You stayed at the western gate.", "E_SOLDIER_GATE|E_QUEEN_SELF|E_DOOR", "F_SOLDIER_GATE|F_QUEEN_LEFT|F_THIEF_ENTERED");
            AddBundle(truth, RoleType.Guard, "You were called away from the east corridor.", "E_GUARD_MARKS|E_FOOTSTEPS|E_DOOR", "F_THIEF_EXITED|F_THIEF_ENTERED");
            AddBundle(truth, RoleType.Servant, "You crossed the west passage and heard footsteps.", "E_SERVANT|E_DOOR|E_CORRIDOR_TRACE", "F_SERVANT_WEST|F_THIEF_ENTERED|F_THIEF_EXITED");
            AddBundle(truth, RoleType.Thief, "During the blackout, you entered the chamber and took the Necklace.", "E_DOOR|E_DISPLAY|E_CASE", "F_THIEF_ENTERED|F_NECKLACE_STOLEN|F_THIEF_EXITED", "Avoid detection");
            return truth;
        }

        private static LocationState Location(string id, bool isPublic, params string[] connected) => new LocationState { LocationId=id, DisplayName=id, IsPublic=isPublic, ConnectedLocationIds=new List<string>(connected) };
        private static FactDefinition Fact(string id, string subject, string predicate, string obj, EvidenceTruth state) => new FactDefinition { FactId=id, SubjectId=subject, Predicate=predicate, ObjectId=obj, Truth=state, Critical=id == "F_THIEF_ENTERED" || id == "F_NECKLACE_STOLEN" || id == "F_THIEF_EXITED" };
        private static EvidenceDefinition Evidence(string id, string description, string fact, string ev, bool independent) => new EvidenceDefinition { EvidenceId=id, Description=description, SupportsFactIds=new List<string> { fact }, RequiredEventIds=new List<string> { ev }, Independent=independent, Critical=true, SourceEventId=ev, SourceId=id, CausalPathId=id };
        private static void AddBundle(ScenarioTruthModel truth, RoleType role, string situation, string evidence, string fact, string objective = "Determine who stole the Necklace")
        {
            truth.PlayerInformation.Add(new PlayerInformationBundle { PlayerId=role.ToString(), Role=role, PrivateSituation=situation, EvidenceIds=new List<string>(evidence.Split('|')), KnownFactIds=new List<string>(fact.Split('|')), Objective=objective });
        }

        private static PlayerScenarioInfo Info(RoleType role, string situation, string clue)
        {
            return new PlayerScenarioInfo { Role = role, PrivateSituation = situation, Clues = new List<string> { clue } };
        }
    }
}
