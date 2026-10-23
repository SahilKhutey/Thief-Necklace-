using System;
using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.Backend;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class FinalReleaseGateTests
    {
        private const string Revision="release-test-revision";
        private static BuildMetadata Build() => new BuildMetadata("thief-necklace", "1.0.0", 100, 7, 3, 2, Revision, "AllProductionTargets");

        private static FinalReleaseAssessment PassingAssessment()
        {
            var domains=Enum.GetValues(typeof(FinalQualityDomain)).Cast<FinalQualityDomain>()
                .Select(domain => new QualityDomainEvidence(domain, QualityEvidenceStatus.Pass, Revision, "artifact/" + domain));
            var roles=new[] { "Game Director", "Gameplay Lead", "Lead Programmer", "Networking Lead", "UI/UX Lead", "QA Lead", "Technical/Build Lead" };
            var signoffs=roles.Select(role => new FinalReleaseSignoff(role, "reviewer", Revision, true, DateTime.UtcNow));
            var checks=new FinalReleaseChecks
            {
                UnitP0Passed=true, UnitP1Passed=true, CriticalSecurityPassed=true, DeterminismPassed=true,
                SerializationPassed=true, ReplayPassed=true, ConfigurationPassed=true, OfflineMatchPassed=true,
                OnlineMatchPassed=true, FullFiveRoundPassed=true, RecoveryPassed=true, RematchPassed=true,
                ScoreIntegrityPassed=true, DebugToolsDisabled=true, ProductionConfigurationFrozen=true,
                TelemetryPrivacyPassed=true, CandidateArtifactVerified=true, RollbackReferenceVerified=true,
                DocumentationComplete=true
            };
            return new FinalReleaseAssessment(Build(), domains, signoffs, new[] { 4, 5, 6, 7, 8 },
                new[] { "Android", "iOS", "WebGL" }, checks, 0, 0, true);
        }

        [Test]
        public void FinalGate_CompleteRevisionMatchedEvidenceApprovesCandidate()
        {
            var report=FinalReleaseGate.Evaluate(PassingAssessment());
            Assert.That(report.Status, Is.EqualTo(FinalReleaseStatus.Approved));
            Assert.That(report.Blockers, Is.Empty);
        }

        [Test]
        public void FinalGate_MissingPlatformAndUnsupportedPlayerCountEvidenceBlocksCandidate()
        {
            var valid=PassingAssessment();
            var incomplete=new FinalReleaseAssessment(valid.Build, valid.Domains, valid.Signoffs, new[] { 4, 5, 6 },
                new[] { "Android", "iOS" }, valid.Checks, 0, 0, true);
            var report=FinalReleaseGate.Evaluate(incomplete);
            Assert.That(report.Status, Is.EqualTo(FinalReleaseStatus.Blocked));
            Assert.That(report.Blockers.Any(item => item.Contains("Player-count validation is missing: 7")), Is.True);
            Assert.That(report.Blockers.Any(item => item.Contains("Platform validation is missing: WebGL")), Is.True);
        }

        [Test]
        public void FinalGate_FailedDomainAndWrongRevisionBlockCandidate()
        {
            var valid=PassingAssessment();
            var domains=valid.Domains.Select(item => item.Domain == FinalQualityDomain.PrivacySecurity
                ? new QualityDomainEvidence(item.Domain, QualityEvidenceStatus.Fail, "old-revision", item.ArtifactReference)
                : item);
            var assessment=new FinalReleaseAssessment(valid.Build, domains, valid.Signoffs, valid.ValidatedPlayerCounts,
                valid.ValidatedPlatforms, valid.Checks, 0, 0, true);
            var report=FinalReleaseGate.Evaluate(assessment);
            Assert.That(report.Approved, Is.False);
            Assert.That(report.Blockers.Any(item => item.Contains("Quality domain is not passing: PrivacySecurity")), Is.True);
            Assert.That(report.Blockers.Any(item => item.Contains("Quality evidence revision does not match the candidate: PrivacySecurity")), Is.True);
        }

        [Test]
        public void FinalGate_P0DefectAndMissingSignoffBlockCandidate()
        {
            var valid=PassingAssessment();
            var assessment=new FinalReleaseAssessment(valid.Build, valid.Domains, valid.Signoffs.Take(6), valid.ValidatedPlayerCounts,
                valid.ValidatedPlatforms, valid.Checks, 1, 0, true);
            var report=FinalReleaseGate.Evaluate(assessment);
            Assert.That(report.Approved, Is.False);
            Assert.That(report.Blockers.Any(item => item.Contains("P0 defect count must be zero")), Is.True);
            Assert.That(report.Blockers.Any(item => item.Contains("Exactly one sign-off is required for: Technical/Build Lead")), Is.True);
        }

        [Test]
        public void FinalLifecycle_FreezesAndReleasesOnlyApprovedBaseline()
        {
            var lifecycle=new FinalReleaseLifecycle();
            lifecycle.BeginDevelopment(); lifecycle.MarkIntegrated(); lifecycle.BeginValidation(); lifecycle.CreateCandidate();
            Assert.Throws<InvalidOperationException>(() => lifecycle.MarkReleased());
            var report=lifecycle.Evaluate(PassingAssessment());
            Assert.That(report.Approved, Is.True);
            var otherRevision=new BuildMetadata("thief-necklace", "1.0.0", 100, 7, 3, 2, "different-revision", "AllProductionTargets");
            Assert.Throws<InvalidOperationException>(() => new ProductionBaseline(otherRevision, report, DateTime.UtcNow));
            var baseline=lifecycle.ApproveAndFreeze(Build(), DateTime.UtcNow);
            Assert.That(baseline.BaselineId, Is.EqualTo("THIEF-NECKLACE-RELEASE-1.0.0"));
            Assert.Throws<InvalidOperationException>(() => new ProductionBaseline(Build(), FinalReleaseGate.Evaluate(null), DateTime.UtcNow));
            lifecycle.MarkReleased();
            lifecycle.MarkRolledBack();
            Assert.That(lifecycle.Status, Is.EqualTo(FinalReleaseStatus.RolledBack));
        }

        [Test]
        public void Handoff_RequiresApprovedBaselineAndCompleteArtifactReferences()
        {
            var valid=PassingAssessment();
            var report=FinalReleaseGate.Evaluate(valid);
            var baseline=new ProductionBaseline(valid.Build, report, DateTime.UtcNow);
            var handoff=new FinalReleaseHandoff(baseline, "sha256:digest", "qa/report", "tests/results", "release/notes",
                "release/known-issues", "rollback/build", new[] { "build/android", "build/ios", "build/webgl" });
            Assert.That(handoff.BuildArtifacts.Count, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => new FinalReleaseHandoff(baseline, "", "qa", "tests", "notes", "issues", "rollback", new[] { "build" }));
        }
    }
}
