using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Live
{
    public enum ScenarioReleaseState { Draft, Validated, Staging, Canary, Live, Watch, Retired, Disabled }
    public enum ScheduledContentState { Upcoming, Active, Ending, Ended }

    [Serializable]
    public sealed class LiveScenarioEntry
    {
        public ThiefAndTheNecklace.ScenarioTemplate template;
        public ScenarioReleaseState state;
        public int weight = 1;
        public bool artComplete;
        public bool audioComplete;
        public bool vfxComplete;
        public bool localizationComplete;
        public string analyticsId;
    }

    [Serializable]
    public sealed class SeasonDefinition
    {
        public string seasonId;
        public string displayName;
        public long startUtcTicks;
        public long endUtcTicks;
        public long endingWindowTicks;
        public string contentVersion;
    }

    [Serializable]
    public sealed class LiveEventDefinition
    {
        public string eventId;
        public string displayName;
        public long startUtcTicks;
        public long endUtcTicks;
        public bool enabled;
        public string[] scenarioIds = new string[0];
    }

    [Serializable]
    public sealed class LiveContentPackage
    {
        public string packageId;
        public string packageHash;
        public int contentVersion;
        public int configurationVersion;
        public string fallbackScenarioId;
        public LiveScenarioEntry[] scenarios = new LiveScenarioEntry[0];
        public SeasonDefinition[] seasons = new SeasonDefinition[0];
        public LiveEventDefinition[] events = new LiveEventDefinition[0];
    }

    public sealed class LiveContentValidation
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid { get { return Errors.Count == 0; } }
    }

    public static class LiveContentValidator
    {
        public static LiveContentValidation Validate(LiveContentPackage package)
        {
            var result = new LiveContentValidation();
            if (package == null) { result.Errors.Add("Content package is missing."); return result; }
            if (string.IsNullOrWhiteSpace(package.packageId) || !IsSha256(package.packageHash) || package.contentVersion <= 0 || package.configurationVersion <= 0)
                result.Errors.Add("Package ID, SHA-256 package hash, and positive content/configuration versions are required.");
            var scenarios = package.scenarios ?? new LiveScenarioEntry[0];
            var entries = scenarios.Where(x => x != null && x.template != null).ToList();
            var scenarioIds = entries.Select(x => x.template.scenarioId).ToList();
            if (entries.Count != scenarios.Length || scenarioIds.Any(string.IsNullOrWhiteSpace) || scenarioIds.Distinct(StringComparer.Ordinal).Count() != scenarioIds.Count)
            { result.Errors.Add("Scenario entries must contain unique, nonempty IDs and templates."); return result; }
            var byId = entries.ToDictionary(x => x.template.scenarioId, x => x, StringComparer.Ordinal);
            var active = entries.Where(IsEligible).ToList();
            if (active.Count == 0 || active.Sum(x => (long)x.weight) <= 0) result.Errors.Add("At least one eligible scenario must have positive total weight.");
            if (!byId.TryGetValue(package.fallbackScenarioId ?? string.Empty, out var fallback) || !IsEligible(fallback))
                result.Errors.Add("Fallback scenario must be present and eligible for new matches.");
            foreach (var entry in entries)
            {
                if (entry.weight < 0 || entry.weight > 10000) result.Errors.Add("Scenario weights must be in the range 0–10000.");
                if (IsEligible(entry))
                {
                    if (!entry.artComplete || !entry.audioComplete || !entry.vfxComplete || !entry.localizationComplete || string.IsNullOrWhiteSpace(entry.analyticsId))
                        result.Errors.Add("Live scenarios require art, audio, VFX, localization, and analytics identifiers.");
                    for (var playerCount = 4; playerCount <= 6; playerCount++)
                    {
                        var validation = ThiefAndTheNecklace.ScenarioTemplateValidator.Validate(entry.template, playerCount);
                        foreach (var error in validation.Errors) result.Errors.Add(entry.template.scenarioId + "/" + playerCount + ": " + error);
                    }
                }
            }
            ValidateSeasons(package.seasons ?? new SeasonDefinition[0], result);
            ValidateEvents(package.events ?? new LiveEventDefinition[0], byId, result);
            return result;
        }

        public static bool IsEligible(LiveScenarioEntry entry) => entry != null && (entry.state == ScenarioReleaseState.Live || entry.state == ScenarioReleaseState.Watch);
        private static bool IsSha256(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        private static void ValidateSeasons(SeasonDefinition[] seasons, LiveContentValidation result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var ordered = seasons.Where(x => x != null).OrderBy(x => x.startUtcTicks).ToList();
            if (ordered.Count != seasons.Length) result.Errors.Add("Season list cannot contain null entries.");
            for (var i = 0; i < ordered.Count; i++)
            {
                var season = ordered[i];
                if (string.IsNullOrWhiteSpace(season.seasonId) || string.IsNullOrWhiteSpace(season.displayName) || string.IsNullOrWhiteSpace(season.contentVersion) || !ids.Add(season.seasonId))
                    result.Errors.Add("Seasons require unique IDs, names, and content versions.");
                if (season.startUtcTicks <= 0 || season.endUtcTicks <= season.startUtcTicks || season.endingWindowTicks < 0 || season.endingWindowTicks > season.endUtcTicks - season.startUtcTicks)
                    result.Errors.Add("Season UTC windows or ending grace duration are invalid.");
                if (i > 0 && ordered[i - 1].endUtcTicks > season.startUtcTicks) result.Errors.Add("Season schedules cannot overlap.");
            }
        }
        private static void ValidateEvents(LiveEventDefinition[] events, Dictionary<string, LiveScenarioEntry> scenarios, LiveContentValidation result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var liveEvent in events)
            {
                if (liveEvent == null) { result.Errors.Add("Event list cannot contain null entries."); continue; }
                if (string.IsNullOrWhiteSpace(liveEvent.eventId) || string.IsNullOrWhiteSpace(liveEvent.displayName) || !ids.Add(liveEvent.eventId))
                    result.Errors.Add("Events require unique IDs and display names.");
                if (liveEvent.startUtcTicks <= 0 || liveEvent.endUtcTicks <= liveEvent.startUtcTicks) result.Errors.Add("Event UTC windows are invalid.");
                foreach (var id in liveEvent.scenarioIds ?? new string[0])
                    if (string.IsNullOrWhiteSpace(id) || !scenarios.ContainsKey(id) || !IsEligible(scenarios.ContainsKey(id) ? scenarios[id] : null)) result.Errors.Add("Event references a scenario that is not eligible: " + id);
            }
        }
    }

    public interface ITrustedUtcClock { long UtcTicks { get; } }

    public sealed class MatchContentPin
    {
        public string MatchId { get; private set; }
        public int ContentVersion { get; private set; }
        public int ConfigurationVersion { get; private set; }
        public string PackageHash { get; private set; }
        public string SeasonId { get; private set; }
        public IReadOnlyList<string> EventIds { get; private set; }
        public string FallbackScenarioId { get; private set; }
        public string ScenarioId { get; private set; }
        public int ScenarioVersion { get; private set; }
        internal MatchContentPin(string matchId, int contentVersion, int configurationVersion, string packageHash, string seasonId, IEnumerable<string> eventIds, string fallbackScenarioId, string scenarioId, int scenarioVersion)
        {
            MatchId=matchId; ContentVersion=contentVersion; ConfigurationVersion=configurationVersion; PackageHash=packageHash; SeasonId=seasonId;
            EventIds=Array.AsReadOnly((eventIds ?? Enumerable.Empty<string>()).ToArray()); FallbackScenarioId=fallbackScenarioId; ScenarioId=scenarioId; ScenarioVersion=scenarioVersion;
        }
    }

    /// <summary>In-memory content control-plane model. Production publication still requires a trusted backend.</summary>
    public sealed class LiveContentService
    {
        private readonly ITrustedUtcClock _clock;
        private Snapshot _active;
        private Snapshot _previous;
        private Snapshot _staged;
        public LiveContentService(ITrustedUtcClock clock) { _clock=clock ?? throw new ArgumentNullException(nameof(clock)); }
        public int ActiveContentVersion { get { return _active == null ? 0 : _active.ContentVersion; } }
        public int ActiveConfigurationVersion { get { return _active == null ? 0 : _active.ConfigurationVersion; } }

        public LiveContentValidation TryStage(LiveContentPackage package)
        {
            var validation = LiveContentValidator.Validate(package);
            if (!validation.IsValid) return validation;
            if (_active != null && (package.contentVersion <= _active.ContentVersion || package.configurationVersion <= _active.ConfigurationVersion))
            { validation.Errors.Add("Staged content and configuration versions must both advance."); return validation; }
            _staged = Snapshot.Create(package);
            return validation;
        }

        public bool ActivateStaged()
        {
            if (_staged == null) return false;
            _previous = _active; _active = _staged; _staged = null; return true;
        }

        // Activates the previous content under a fresh monotonically increasing config version.
        public bool RollbackForNewMatches()
        {
            if (_previous == null || _active == null) return false;
            var old = _active;
            _active = _previous.WithConfigurationVersion(old.ConfigurationVersion + 1);
            _previous = old;
            _staged = null;
            return true;
        }

        public bool TryPinMatch(string matchId, int seed, out MatchContentPin pin)
        {
            pin = null;
            if (_active == null || string.IsNullOrWhiteSpace(matchId)) return false;
            var now = _clock.UtcTicks;
            var season = _active.Seasons.Where(x => x.startUtcTicks <= now && now < x.endUtcTicks).OrderBy(x => x.startUtcTicks).FirstOrDefault();
            var activeEvents = _active.Events.Where(x => x.enabled && x.startUtcTicks <= now && now < x.endUtcTicks).ToList();
            var eventIds = activeEvents.Select(x => x.eventId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var eventScenarioIds = activeEvents.SelectMany(x => x.scenarioIds ?? new string[0]).Distinct(StringComparer.Ordinal).ToArray();
            var scenarioId = _active.Select(seed, eventScenarioIds, out var scenarioVersion);
            pin = new MatchContentPin(matchId, _active.ContentVersion, _active.ConfigurationVersion, _active.PackageHash, season == null ? null : season.seasonId, eventIds, _active.FallbackScenarioId, scenarioId, scenarioVersion);
            return true;
        }

        private sealed class Snapshot
        {
            public int ContentVersion;
            public int ConfigurationVersion;
            public string PackageHash;
            public string FallbackScenarioId;
            public Dictionary<string, int> Weights;
            public Dictionary<string, int> ScenarioVersions;
            public SeasonDefinition[] Seasons;
            public LiveEventDefinition[] Events;
            public static Snapshot Create(LiveContentPackage package) => new Snapshot
            {
                ContentVersion=package.contentVersion, ConfigurationVersion=package.configurationVersion, PackageHash=package.packageHash, FallbackScenarioId=package.fallbackScenarioId,
                Weights=package.scenarios.Where(LiveContentValidator.IsEligible).Where(x => x.weight > 0).ToDictionary(x => x.template.scenarioId, x => x.weight, StringComparer.Ordinal),
                ScenarioVersions=package.scenarios.Where(LiveContentValidator.IsEligible).ToDictionary(x => x.template.scenarioId, x => x.template.version, StringComparer.Ordinal),
                Seasons=(package.seasons ?? new SeasonDefinition[0]).Select(CloneSeason).ToArray(),
                Events=(package.events ?? new LiveEventDefinition[0]).Select(CloneEvent).ToArray()
            };
            public Snapshot WithConfigurationVersion(int version) => new Snapshot
            {
                ContentVersion=ContentVersion, ConfigurationVersion=version, PackageHash=PackageHash, FallbackScenarioId=FallbackScenarioId,
                Weights=new Dictionary<string, int>(Weights, StringComparer.Ordinal), ScenarioVersions=new Dictionary<string, int>(ScenarioVersions, StringComparer.Ordinal), Seasons=Seasons.Select(CloneSeason).ToArray(), Events=Events.Select(CloneEvent).ToArray()
            };
            public string Select(int seed, IEnumerable<string> preferredScenarioIds, out int scenarioVersion)
            {
                var preferred = new HashSet<string>(preferredScenarioIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                var filtered = preferred.Count == 0 ? Weights : Weights.Where(x => preferred.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
                var ordered=filtered.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
                if (ordered.Count == 0) { scenarioVersion=ScenarioVersions[FallbackScenarioId]; return FallbackScenarioId; }
                var total=(ulong)ordered.Sum(x => (long)x.Value);
                var slot=((ulong)(uint)seed * total) >> 32;
                foreach (var entry in ordered) { if (slot < (ulong)entry.Value) { scenarioVersion=ScenarioVersions[entry.Key]; return entry.Key; } slot -= (ulong)entry.Value; }
                scenarioVersion=ScenarioVersions[FallbackScenarioId]; return FallbackScenarioId;
            }
            private static SeasonDefinition CloneSeason(SeasonDefinition x) => new SeasonDefinition { seasonId=x.seasonId, displayName=x.displayName, startUtcTicks=x.startUtcTicks, endUtcTicks=x.endUtcTicks, endingWindowTicks=x.endingWindowTicks, contentVersion=x.contentVersion };
            private static LiveEventDefinition CloneEvent(LiveEventDefinition x) => new LiveEventDefinition { eventId=x.eventId, displayName=x.displayName, startUtcTicks=x.startUtcTicks, endUtcTicks=x.endUtcTicks, enabled=x.enabled, scenarioIds=(x.scenarioIds ?? new string[0]).ToArray() };
        }
    }

    public static class SeasonSchedule
    {
        public static ScheduledContentState Resolve(SeasonDefinition season, long trustedUtcTicks)
        {
            if (season == null || trustedUtcTicks < season.startUtcTicks) return ScheduledContentState.Upcoming;
            if (trustedUtcTicks >= season.endUtcTicks) return ScheduledContentState.Ended;
            return season.endingWindowTicks > 0 && trustedUtcTicks >= season.endUtcTicks - season.endingWindowTicks
                ? ScheduledContentState.Ending : ScheduledContentState.Active;
        }
    }
}
