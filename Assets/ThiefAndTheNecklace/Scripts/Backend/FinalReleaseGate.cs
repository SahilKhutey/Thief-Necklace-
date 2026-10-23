using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Backend
{
    public enum FinalReleaseStatus { NotStarted, Development, Integration, Validation, ReleaseCandidate, Approved, Blocked, Released, RolledBack }
    public enum FinalQualityDomain
    {
        Gameplay, Networking, Recovery, PrivacySecurity, UserExperience, Accessibility,
        Performance, Replay, Diagnostics, Platform, Build, Operations, Documentation
    }
    public enum QualityEvidenceStatus { Pass, Fail, Blocked, NotApplicable }

    public sealed class QualityDomainEvidence
    {
        public FinalQualityDomain Domain { get; }
        public QualityEvidenceStatus Status { get; }
        public string SourceRevision { get; }
        public string ArtifactReference { get; }
        public string NotApplicableReason { get; }

        public QualityDomainEvidence(FinalQualityDomain domain, QualityEvidenceStatus status, string sourceRevision,
            string artifactReference, string notApplicableReason = null)
        {
            if (!Enum.IsDefined(typeof(FinalQualityDomain), domain)) throw new ArgumentOutOfRangeException(nameof(domain));
            if (!Enum.IsDefined(typeof(QualityEvidenceStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            Domain=domain; Status=status; SourceRevision=sourceRevision ?? string.Empty;
            ArtifactReference=artifactReference ?? string.Empty; NotApplicableReason=notApplicableReason ?? string.Empty;
        }
    }

    public sealed class FinalReleaseSignoff
    {
        public string Role { get; }
        public string Approver { get; }
        public string SourceRevision { get; }
        public bool Approved { get; }
        public DateTime SignedAtUtc { get; }

        public FinalReleaseSignoff(string role, string approver, string sourceRevision, bool approved, DateTime signedAtUtc)
        {
            Role=role ?? string.Empty; Approver=approver ?? string.Empty; SourceRevision=sourceRevision ?? string.Empty;
            Approved=approved; SignedAtUtc=signedAtUtc == default(DateTime) || signedAtUtc.Kind == DateTimeKind.Utc ? signedAtUtc : signedAtUtc.ToUniversalTime();
        }
    }

    /// <summary>Concrete verification gates; a true value is evidence supplied by CI/QA, never inferred by this model.</summary>
    public sealed class FinalReleaseChecks
    {
        public bool UnitP0Passed { get; set; }
        public bool UnitP1Passed { get; set; }
        public bool CriticalSecurityPassed { get; set; }
        public bool DeterminismPassed { get; set; }
        public bool SerializationPassed { get; set; }
        public bool ReplayPassed { get; set; }
        public bool ConfigurationPassed { get; set; }
        public bool OfflineMatchPassed { get; set; }
        public bool OnlineMatchPassed { get; set; }
        public bool FullFiveRoundPassed { get; set; }
        public bool RecoveryPassed { get; set; }
        public bool RematchPassed { get; set; }
        public bool ScoreIntegrityPassed { get; set; }
        public bool DebugToolsDisabled { get; set; }
        public bool ProductionConfigurationFrozen { get; set; }
        public bool TelemetryPrivacyPassed { get; set; }
        public bool CandidateArtifactVerified { get; set; }
        public bool RollbackReferenceVerified { get; set; }
        public bool DocumentationComplete { get; set; }

        internal FinalReleaseChecks Copy() => (FinalReleaseChecks)MemberwiseClone();
        internal IEnumerable<string> GetFailures()
        {
            if (!UnitP0Passed) yield return "GW17 P0 unit tests have not passed.";
            if (!UnitP1Passed) yield return "GW17 P1 unit tests have not passed.";
            if (!CriticalSecurityPassed) yield return "Critical security validation has not passed.";
            if (!DeterminismPassed) yield return "Determinism validation has not passed.";
            if (!SerializationPassed) yield return "Serialization validation has not passed.";
            if (!ReplayPassed) yield return "Replay validation has not passed.";
            if (!ConfigurationPassed) yield return "Configuration validation has not passed.";
            if (!OfflineMatchPassed) yield return "Offline match validation has not passed.";
            if (!OnlineMatchPassed) yield return "Online match validation has not passed.";
            if (!FullFiveRoundPassed) yield return "Full five-round validation has not passed.";
            if (!RecoveryPassed) yield return "Recovery validation has not passed.";
            if (!RematchPassed) yield return "Rematch validation has not passed.";
            if (!ScoreIntegrityPassed) yield return "Displayed, authoritative, and ledger scores have not been reconciled.";
            if (!DebugToolsDisabled) yield return "Production debug and mutation tools are not confirmed disabled.";
            if (!ProductionConfigurationFrozen) yield return "Production configuration is not frozen.";
            if (!TelemetryPrivacyPassed) yield return "Production telemetry privacy has not passed review.";
            if (!CandidateArtifactVerified) yield return "Release candidate artifact has not been verified.";
            if (!RollbackReferenceVerified) yield return "A verified rollback reference is missing.";
            if (!DocumentationComplete) yield return "Required handoff documentation is incomplete.";
        }
    }

    public sealed class FinalReleaseAssessment
    {
        public BuildMetadata Build { get; }
        public IReadOnlyList<QualityDomainEvidence> Domains { get; }
        public IReadOnlyList<FinalReleaseSignoff> Signoffs { get; }
        public IReadOnlyList<int> ValidatedPlayerCounts { get; }
        public IReadOnlyList<string> ValidatedPlatforms { get; }
        public FinalReleaseChecks Checks { get; }
        public int P0Defects { get; }
        public int CriticalP1Defects { get; }
        public bool GameplayIntegrityHealthy { get; }

        public FinalReleaseAssessment(BuildMetadata build, IEnumerable<QualityDomainEvidence> domains,
            IEnumerable<FinalReleaseSignoff> signoffs, IEnumerable<int> validatedPlayerCounts,
            IEnumerable<string> validatedPlatforms, FinalReleaseChecks checks, int p0Defects,
            int criticalP1Defects, bool gameplayIntegrityHealthy)
        {
            Build=build;
            Domains=Array.AsReadOnly((domains ?? Enumerable.Empty<QualityDomainEvidence>()).Where(item => item != null).ToArray());
            Signoffs=Array.AsReadOnly((signoffs ?? Enumerable.Empty<FinalReleaseSignoff>()).Where(item => item != null).ToArray());
            ValidatedPlayerCounts=Array.AsReadOnly((validatedPlayerCounts ?? Enumerable.Empty<int>()).Distinct().OrderBy(value => value).ToArray());
            ValidatedPlatforms=Array.AsReadOnly((validatedPlatforms ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
            Checks=checks?.Copy(); P0Defects=p0Defects; CriticalP1Defects=criticalP1Defects; GameplayIntegrityHealthy=gameplayIntegrityHealthy;
        }
    }

    public sealed class FinalReleaseReport
    {
        public FinalReleaseStatus Status { get; }
        public IReadOnlyList<string> Blockers { get; }
        public string SourceRevision { get; }
        public bool Approved => Status == FinalReleaseStatus.Approved;
        internal FinalReleaseReport(FinalReleaseStatus status, IEnumerable<string> blockers, string sourceRevision)
        { Status=status; Blockers=Array.AsReadOnly(blockers.ToArray()); SourceRevision=sourceRevision ?? string.Empty; }
    }

    public static class FinalReleaseGate
    {
        private static readonly FinalQualityDomain[] RequiredDomains=(FinalQualityDomain[])Enum.GetValues(typeof(FinalQualityDomain));
        private static readonly string[] RequiredApproverRoles={"Game Director", "Gameplay Lead", "Lead Programmer", "Networking Lead", "UI/UX Lead", "QA Lead", "Technical/Build Lead"};
        private static readonly int[] RequiredPlayerCounts={4,5,6,7,8};
        private static readonly string[] RequiredPlatforms={"Android", "iOS", "WebGL"};

        public static FinalReleaseReport Evaluate(FinalReleaseAssessment assessment)
        {
            var blockers=new List<string>();
            if (assessment == null) return new FinalReleaseReport(FinalReleaseStatus.Blocked, new[] { "Final release assessment is missing." }, string.Empty);
            if (assessment.Build == null) blockers.Add("Build metadata is missing.");
            else blockers.AddRange(assessment.Build.Validate());
            if (assessment.P0Defects != 0) blockers.Add("P0 defect count must be zero.");
            if (assessment.CriticalP1Defects != 0) blockers.Add("Critical P1 defect count must be zero.");
            if (assessment.P0Defects < 0 || assessment.CriticalP1Defects < 0) blockers.Add("Defect counts cannot be negative.");
            if (!assessment.GameplayIntegrityHealthy) blockers.Add("Gameplay integrity is unhealthy.");

            var revision=assessment.Build?.SourceRevision ?? string.Empty;
            var domainGroups=assessment.Domains.GroupBy(item => item.Domain).ToDictionary(group => group.Key, group => group.ToArray());
            foreach (var domain in RequiredDomains)
            {
                QualityDomainEvidence[] entries;
                if (!domainGroups.TryGetValue(domain, out entries)) { blockers.Add("Quality domain evidence is missing: " + domain + "."); continue; }
                if (entries.Length != 1) { blockers.Add("Quality domain must have exactly one current result: " + domain + "."); continue; }
                var evidence=entries[0];
                if (evidence.Status == QualityEvidenceStatus.Fail || evidence.Status == QualityEvidenceStatus.Blocked)
                    blockers.Add("Quality domain is not passing: " + domain + " (" + evidence.Status + ").");
                if (evidence.Status == QualityEvidenceStatus.NotApplicable && string.IsNullOrWhiteSpace(evidence.NotApplicableReason))
                    blockers.Add("Not-applicable quality domain needs a documented rationale: " + domain + ".");
                if (!string.Equals(evidence.SourceRevision, revision, StringComparison.Ordinal)) blockers.Add("Quality evidence revision does not match the candidate: " + domain + ".");
                if (string.IsNullOrWhiteSpace(evidence.ArtifactReference)) blockers.Add("Quality evidence artifact is missing: " + domain + ".");
            }
            if (assessment.Domains.Any(item => !Enum.IsDefined(typeof(FinalQualityDomain), item.Domain))) blockers.Add("Unknown quality domain was supplied.");

            foreach (var count in RequiredPlayerCounts)
                if (!assessment.ValidatedPlayerCounts.Contains(count)) blockers.Add("Player-count validation is missing: " + count + ".");
            foreach (var platform in RequiredPlatforms)
                if (!assessment.ValidatedPlatforms.Contains(platform, StringComparer.OrdinalIgnoreCase)) blockers.Add("Platform validation is missing: " + platform + ".");

            if (assessment.Checks == null) blockers.Add("Final verification checklist is missing.");
            else blockers.AddRange(assessment.Checks.GetFailures());
            ValidateSignoffs(assessment.Signoffs, revision, blockers);
            return blockers.Count == 0
                ? new FinalReleaseReport(FinalReleaseStatus.Approved, blockers, revision)
                : new FinalReleaseReport(FinalReleaseStatus.Blocked, blockers.Distinct(StringComparer.Ordinal).ToArray(), revision);
        }

        private static void ValidateSignoffs(IReadOnlyList<FinalReleaseSignoff> signoffs, string revision, List<string> blockers)
        {
            foreach (var role in RequiredApproverRoles)
            {
                var entries=signoffs.Where(item => string.Equals(item.Role, role, StringComparison.Ordinal)).ToArray();
                if (entries.Length != 1) { blockers.Add("Exactly one sign-off is required for: " + role + "."); continue; }
                var entry=entries[0];
                if (string.IsNullOrWhiteSpace(entry.Approver)) blockers.Add("Approver identity is missing for: " + role + ".");
                if (!entry.Approved) blockers.Add("Sign-off was not approved for: " + role + ".");
                if (!string.Equals(entry.SourceRevision, revision, StringComparison.Ordinal)) blockers.Add("Sign-off revision does not match the candidate for: " + role + ".");
                if (entry.SignedAtUtc == default(DateTime)) blockers.Add("Sign-off timestamp is missing for: " + role + ".");
            }
        }
    }

    /// <summary>Creates a frozen source-baseline identity only from a fully approved final report.</summary>
    public sealed class ProductionBaseline
    {
        public string BaselineId { get; }
        public string SourceRevision { get; }
        public string GameVersion { get; }
        public int BuildNumber { get; }
        public int ProtocolVersion { get; }
        public int RulesVersion { get; }
        public int ScenarioVersion { get; }
        public DateTime FrozenAtUtc { get; }

        public ProductionBaseline(BuildMetadata build, FinalReleaseReport report, DateTime frozenAtUtc)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (report == null || !report.Approved) throw new InvalidOperationException("Only an approved final release can be frozen as the production baseline.");
            if (!string.Equals(build.SourceRevision, report.SourceRevision, StringComparison.Ordinal))
                throw new InvalidOperationException("The approved evidence revision must match the baseline source revision.");
            BaselineId="THIEF-NECKLACE-RELEASE-" + build.GameVersion;
            SourceRevision=build.SourceRevision; GameVersion=build.GameVersion; BuildNumber=build.BuildNumber;
            ProtocolVersion=build.NetworkProtocolVersion; RulesVersion=build.RulesVersion; ScenarioVersion=build.ScenarioVersion;
            FrozenAtUtc=frozenAtUtc.Kind == DateTimeKind.Utc ? frozenAtUtc : frozenAtUtc.ToUniversalTime();
        }
    }

    public sealed class FinalReleaseLifecycle
    {
        public FinalReleaseStatus Status { get; private set; } = FinalReleaseStatus.NotStarted;
        public FinalReleaseReport LastReport { get; private set; }
        public ProductionBaseline Baseline { get; private set; }
        public string BlockReason { get; private set; } = string.Empty;

        public void BeginDevelopment() { Move(FinalReleaseStatus.NotStarted, FinalReleaseStatus.Development); }
        public void MarkIntegrated() { Move(FinalReleaseStatus.Development, FinalReleaseStatus.Integration); }
        public void BeginValidation() { Move(FinalReleaseStatus.Integration, FinalReleaseStatus.Validation); }
        public void CreateCandidate() { Move(FinalReleaseStatus.Validation, FinalReleaseStatus.ReleaseCandidate); }
        public FinalReleaseReport Evaluate(FinalReleaseAssessment assessment)
        {
            Require(FinalReleaseStatus.ReleaseCandidate);
            LastReport=FinalReleaseGate.Evaluate(assessment);
            if (!LastReport.Approved) { Status=FinalReleaseStatus.Blocked; BlockReason=string.Join("; ", LastReport.Blockers); }
            return LastReport;
        }
        public ProductionBaseline ApproveAndFreeze(BuildMetadata build, DateTime frozenAtUtc)
        {
            Require(FinalReleaseStatus.ReleaseCandidate);
            if (LastReport == null || !LastReport.Approved) throw new InvalidOperationException("A passing final gate report is required before approval.");
            Baseline=new ProductionBaseline(build, LastReport, frozenAtUtc);
            Status=FinalReleaseStatus.Approved; BlockReason=string.Empty;
            return Baseline;
        }
        public void MarkReleased() { Move(FinalReleaseStatus.Approved, FinalReleaseStatus.Released); }
        public void MarkRolledBack() { Move(FinalReleaseStatus.Released, FinalReleaseStatus.RolledBack); }
        public void ReevaluateAfterBlock() { Move(FinalReleaseStatus.Blocked, FinalReleaseStatus.Validation); BlockReason=string.Empty; LastReport=null; }

        private void Move(FinalReleaseStatus expected, FinalReleaseStatus next) { Require(expected); Status=next; }
        private void Require(FinalReleaseStatus expected)
        { if (Status != expected) throw new InvalidOperationException("Expected final release status " + expected + ", current status is " + Status + "."); }
    }

    public sealed class FinalReleaseHandoff
    {
        public ProductionBaseline Baseline { get; }
        public string ConfigurationDigest { get; }
        public string QaReportReference { get; }
        public string TestResultsReference { get; }
        public string ReleaseNotesReference { get; }
        public string KnownIssuesReference { get; }
        public string RollbackReference { get; }
        public IReadOnlyList<string> BuildArtifacts { get; }

        public FinalReleaseHandoff(ProductionBaseline baseline, string configurationDigest, string qaReportReference,
            string testResultsReference, string releaseNotesReference, string knownIssuesReference,
            string rollbackReference, IEnumerable<string> buildArtifacts)
        {
            Baseline=baseline ?? throw new ArgumentNullException(nameof(baseline));
            ConfigurationDigest=RequireToken(configurationDigest, nameof(configurationDigest));
            QaReportReference=RequireToken(qaReportReference, nameof(qaReportReference));
            TestResultsReference=RequireToken(testResultsReference, nameof(testResultsReference));
            ReleaseNotesReference=RequireToken(releaseNotesReference, nameof(releaseNotesReference));
            KnownIssuesReference=RequireToken(knownIssuesReference, nameof(knownIssuesReference));
            RollbackReference=RequireToken(rollbackReference, nameof(rollbackReference));
            var artifacts=(buildArtifacts ?? Enumerable.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
            if (artifacts.Length == 0) throw new ArgumentException("At least one verified build artifact is required.", nameof(buildArtifacts));
            BuildArtifacts=Array.AsReadOnly(artifacts);
        }
        private static string RequireToken(string value, string name)
        { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Handoff reference is required.", name); return value.Trim(); }
    }
}
