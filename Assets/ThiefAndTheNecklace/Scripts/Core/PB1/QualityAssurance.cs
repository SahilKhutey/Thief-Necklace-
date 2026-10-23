using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public enum QaTestStatus { NotRun, Passed, Failed, Blocked, Skipped }
    public enum QaTestCategory { Unit, PlayMode, Integration, Network, Recovery, Security, UX, Performance, Platform, Regression, Release }
    public enum DefectSeverity { P0Critical, P1High, P2Medium, P3Low }
    public enum DefectStatus { New, Triaged, Assigned, Fixed, Retest, Verified, Closed, Reopened }

    /// <summary>Portable case metadata used by QA reports, CI adapters, and manual test runs.</summary>
    public sealed class QaTestCase
    {
        public string Id { get; }
        public string Name { get; }
        public string Preconditions { get; }
        public IReadOnlyList<string> Steps { get; }
        public string ExpectedResult { get; }
        public DefectSeverity Severity { get; }
        public IReadOnlyList<QaTestCategory> Categories { get; }

        public QaTestCase(string id, string name, string preconditions, IEnumerable<string> steps,
            string expectedResult, DefectSeverity severity, IEnumerable<QaTestCategory> categories)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable test ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A test name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(expectedResult)) throw new ArgumentException("An expected result is required.", nameof(expectedResult));
            var stepCopy = (steps ?? Enumerable.Empty<string>()).ToArray();
            if (stepCopy.Length == 0 || stepCopy.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("At least one non-empty test step is required.", nameof(steps));
            var categoryCopy = (categories ?? Enumerable.Empty<QaTestCategory>()).Distinct().ToArray();
            if (categoryCopy.Length == 0) throw new ArgumentException("At least one test category is required.", nameof(categories));
            Id = id.Trim(); Name = name.Trim(); Preconditions = preconditions ?? string.Empty;
            Steps = Array.AsReadOnly(stepCopy); ExpectedResult = expectedResult.Trim(); Severity = severity;
            Categories = Array.AsReadOnly(categoryCopy);
        }
    }

    public sealed class QaTestResult
    {
        public string TestId { get; }
        public QaTestStatus Status { get; }
        public long DurationMs { get; }
        public string FailureMessage { get; }
        public string BugId { get; }
        public string ReplayId { get; }
        public string StateHash { get; }

        public QaTestResult(string testId, QaTestStatus status, long durationMs, string failureMessage = null,
            string bugId = null, string replayId = null, string stateHash = null)
        {
            if (string.IsNullOrWhiteSpace(testId)) throw new ArgumentException("A test ID is required.", nameof(testId));
            if (durationMs < 0) throw new ArgumentOutOfRangeException(nameof(durationMs));
            if (status == QaTestStatus.Failed && string.IsNullOrWhiteSpace(failureMessage))
                throw new ArgumentException("Failed results must include an observed failure.", nameof(failureMessage));
            TestId = testId.Trim(); Status = status; DurationMs = durationMs;
            FailureMessage = failureMessage ?? string.Empty; BugId = bugId ?? string.Empty;
            ReplayId = replayId ?? string.Empty; StateHash = stateHash ?? string.Empty;
        }
    }

    public sealed class QaTestRun
    {
        public string BuildVersion { get; }
        public string Platform { get; }
        public DateTimeOffset StartedAt { get; }
        public IReadOnlyList<QaTestResult> Results { get; }
        public bool Passed => Results.Count > 0 && Results.All(r => r.Status == QaTestStatus.Passed);

        public QaTestRun(string buildVersion, string platform, DateTimeOffset startedAt, IEnumerable<QaTestResult> results)
        {
            if (string.IsNullOrWhiteSpace(buildVersion)) throw new ArgumentException("A build version is required.", nameof(buildVersion));
            if (string.IsNullOrWhiteSpace(platform)) throw new ArgumentException("A platform is required.", nameof(platform));
            var copy = (results ?? Enumerable.Empty<QaTestResult>()).ToArray();
            if (copy.Any(r => r == null)) throw new ArgumentException("Test results cannot contain null entries.", nameof(results));
            if (copy.Select(r => r.TestId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("A test may appear only once in a run.", nameof(results));
            BuildVersion = buildVersion.Trim(); Platform = platform.Trim(); StartedAt = startedAt;
            Results = Array.AsReadOnly(copy);
        }
    }

    public sealed class ReleaseSignoff
    {
        public string BuildVersion { get; }
        public bool GameplayApproved { get; }
        public bool NetworkingApproved { get; }
        public bool QaApproved { get; }
        public bool UxApproved { get; }
        public bool PerformanceApproved { get; }
        public bool SecurityApproved { get; }
        public bool PlatformApproved { get; }
        public bool Approved => GameplayApproved && NetworkingApproved && QaApproved && UxApproved &&
            PerformanceApproved && SecurityApproved && PlatformApproved;

        public ReleaseSignoff(string buildVersion, bool gameplayApproved, bool networkingApproved, bool qaApproved,
            bool uxApproved, bool performanceApproved, bool securityApproved, bool platformApproved)
        {
            if (string.IsNullOrWhiteSpace(buildVersion)) throw new ArgumentException("A build version is required.", nameof(buildVersion));
            BuildVersion = buildVersion.Trim(); GameplayApproved = gameplayApproved; NetworkingApproved = networkingApproved;
            QaApproved = qaApproved; UxApproved = uxApproved; PerformanceApproved = performanceApproved;
            SecurityApproved = securityApproved; PlatformApproved = platformApproved;
        }
    }

    public sealed class ReleaseGateInput
    {
        public string BuildVersion { get; }
        public QaTestRun Run { get; }
        public int OpenP0Defects { get; }
        public int OpenP1Defects { get; }
        public ReleaseSignoff Signoff { get; }
        public bool BuildValidated { get; }
        public bool RequiredPlayerCountMatrixComplete { get; }

        public ReleaseGateInput(string buildVersion, QaTestRun run, int openP0Defects, int openP1Defects,
            ReleaseSignoff signoff, bool buildValidated, bool requiredPlayerCountMatrixComplete)
        {
            if (string.IsNullOrWhiteSpace(buildVersion)) throw new ArgumentException("A build version is required.", nameof(buildVersion));
            if (openP0Defects < 0) throw new ArgumentOutOfRangeException(nameof(openP0Defects));
            if (openP1Defects < 0) throw new ArgumentOutOfRangeException(nameof(openP1Defects));
            BuildVersion = buildVersion.Trim(); Run = run; OpenP0Defects = openP0Defects; OpenP1Defects = openP1Defects;
            Signoff = signoff; BuildValidated = buildValidated; RequiredPlayerCountMatrixComplete = requiredPlayerCountMatrixComplete;
        }
    }

    public sealed class ReleaseGateDecision
    {
        public bool Approved { get; }
        public IReadOnlyList<string> Blockers { get; }
        internal ReleaseGateDecision(IEnumerable<string> blockers)
        {
            var copy = blockers.ToArray(); Blockers = Array.AsReadOnly(copy); Approved = copy.Length == 0;
        }
    }

    public sealed class ReleaseGateEvaluator
    {
        public ReleaseGateDecision Evaluate(ReleaseGateInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var blockers = new List<string>();
            if (input.Run == null) blockers.Add("QA test run is missing.");
            else
            {
                if (!string.Equals(input.Run.BuildVersion, input.BuildVersion, StringComparison.Ordinal)) blockers.Add("QA run build does not match the release build.");
                if (!input.Run.Passed) blockers.Add("QA run contains failed, blocked, skipped, not-run, or no test results.");
            }
            if (input.OpenP0Defects != 0) blockers.Add("Open P0 defects must be zero.");
            if (input.OpenP1Defects != 0) blockers.Add("Open P1 defects must be resolved before release approval.");
            if (!input.BuildValidated) blockers.Add("Build validation is incomplete.");
            if (!input.RequiredPlayerCountMatrixComplete) blockers.Add("Required player-count coverage is incomplete.");
            if (input.Signoff == null) blockers.Add("Release sign-off is missing.");
            else
            {
                if (!string.Equals(input.Signoff.BuildVersion, input.BuildVersion, StringComparison.Ordinal)) blockers.Add("Sign-off build does not match the release build.");
                if (!input.Signoff.Approved) blockers.Add("One or more required sign-off domains are not approved.");
            }
            return new ReleaseGateDecision(blockers);
        }
    }

    public sealed class DefectRecord
    {
        public string Id { get; }
        public DefectSeverity Severity { get; }
        public DefectStatus Status { get; private set; }
        public string ReplayId { get; private set; }
        public string TraceId { get; private set; }

        public DefectRecord(string id, DefectSeverity severity)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A defect ID is required.", nameof(id));
            Id = id.Trim(); Severity = severity; Status = DefectStatus.New;
        }

        public void TransitionTo(DefectStatus next, string replayId = null, string traceId = null)
        {
            if (next == DefectStatus.Reopened)
            {
                if (Status != DefectStatus.Verified) throw new InvalidOperationException("Only a verified defect can be reopened.");
                if (string.IsNullOrWhiteSpace(replayId) || string.IsNullOrWhiteSpace(traceId))
                    throw new InvalidOperationException("Reopening a verified defect requires replay and trace evidence.");
                ReplayId = replayId.Trim(); TraceId = traceId.Trim(); Status = next; return;
            }
            var allowed = (Status == DefectStatus.New && next == DefectStatus.Triaged) ||
                (Status == DefectStatus.Triaged && next == DefectStatus.Assigned) ||
                (Status == DefectStatus.Assigned && next == DefectStatus.Fixed) ||
                (Status == DefectStatus.Fixed && next == DefectStatus.Retest) ||
                (Status == DefectStatus.Retest && next == DefectStatus.Verified) ||
                (Status == DefectStatus.Verified && next == DefectStatus.Closed) ||
                (Status == DefectStatus.Reopened && next == DefectStatus.Triaged);
            if (!allowed) throw new InvalidOperationException("Illegal defect transition " + Status + " -> " + next + ".");
            Status = next;
        }
    }
}
