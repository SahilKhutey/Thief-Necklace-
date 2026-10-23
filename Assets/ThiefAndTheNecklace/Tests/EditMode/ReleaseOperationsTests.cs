using System;
using System.Collections.Generic;
using NUnit.Framework;
using ThiefAndTheNecklace.Backend;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class ReleaseOperationsTests
    {
        private static BuildMetadata Build() => new BuildMetadata("thief-necklace-prod", "1.2.0", 12, 7, 3, 2, "abc123", "WebGL");
        private static ReleaseValidationEvidence PassedEvidence() => new ReleaseValidationEvidence
        {
            CandidateBuilt=true, IntegrationPassed=true, RegressionPassed=true, MultiplayerPassed=true, RecoveryPassed=true,
            SecurityPassed=true, PlatformPassed=true, PerformancePassed=true, ConfigurationFrozen=true,
            RollbackAvailable=true, MonitoringReady=true, GameplayIntegrityHealthy=true
        };

        [Test]
        public void MatchContractPinsBuildProtocolRulesAndScenarioVersions()
        {
            var match=new VersionedMatchContract("match-1", Build(), 3, 2, 9001);
            var compatible=new VersionedMatchContract("match-2", Build(), 3, 2, 4);
            var drifted=new VersionedMatchContract("match-3", Build(), 4, 2, 4);
            Assert.That(match.IsCompatible(compatible), Is.True);
            Assert.That(match.IsCompatible(drifted), Is.False);
        }

        [Test]
        public void FrozenConfigurationCopiesValuesAndRejectsSecretMaterial()
        {
            var source=new Dictionary<string, string> { ["region"]="eu" };
            var frozen=new FrozenProductionConfiguration(ReleaseChannel.Staging, source);
            source["region"]="us";
            Assert.That(frozen.Values["region"], Is.EqualTo("eu"));
            Assert.That(frozen.Digest, Has.Length.EqualTo(64));
            Assert.Throws<ArgumentException>(() => new FrozenProductionConfiguration(ReleaseChannel.Production,
                new[] { new KeyValuePair<string, string>("access_token", "do-not-store") }));
        }

        [Test]
        public void AnalyticsQueueIsBoundedAndDoesNotCarryPrivatePayloadFields()
        {
            var queue=new SafeAnalyticsQueue(1);
            var evt=new SafeAnalyticsEvent(AnalyticsEventKind.MatchCompleted, DateTime.UtcNow, "1.2.0", "WebGL", 4, 1250, AnalyticsResultCode.Success);
            Assert.That(queue.TryEnqueue(evt), Is.True);
            Assert.That(queue.TryEnqueue(evt), Is.False);
            Assert.That(queue.DroppedCount, Is.EqualTo(1));
            SafeAnalyticsEvent dequeued;
            Assert.That(queue.TryDequeue(out dequeued), Is.True);
            Assert.That(dequeued.ResultCode, Is.EqualTo(AnalyticsResultCode.Success));
            Assert.Throws<ArgumentException>(() => new SafeAnalyticsEvent(AnalyticsEventKind.ClientError, DateTime.UtcNow,
                "1.2.0", "WebGL", resultCode: (AnalyticsResultCode)999));
        }

        [Test]
        public void FeatureRolloutIsStableAndReleaseNeedsEveryGate()
        {
            var flags=new FeatureFlagSet(new[] { new FeatureFlag("new-results", true, 50) });
            Assert.That(flags.IsEnabled("new-results", "anonymous-a"), Is.EqualTo(flags.IsEnabled("new-results", "anonymous-a")));
            var gate=new ReleaseValidationEvidence
            {
                CandidateBuilt=true, IntegrationPassed=true, RegressionPassed=true, MultiplayerPassed=true, RecoveryPassed=true,
                SecurityPassed=true, PlatformPassed=true, PerformancePassed=true, ConfigurationFrozen=true,
                RollbackAvailable=true, MonitoringReady=true, GameplayIntegrityHealthy=false, P0Incidents=0, CriticalP1Incidents=0
            };
            Assert.That(ReleaseCandidateGate.Validate(Build(), gate), Does.Contain("Gameplay integrity is unhealthy."));
        }

        [Test]
        public void DeploymentAndRollbackRequireSmokeAndCompatibility()
        {
            var release=new ReleaseController(Build(), PassedEvidence());
            release.BeginDeployment();
            Assert.Throws<InvalidOperationException>(() => release.MarkLive(false));
            release.MarkLive(true);
            release.MarkDegraded();
            release.BeginRollback();
            Assert.Throws<InvalidOperationException>(() => release.CompleteRollback(false, true));
            Assert.Throws<InvalidOperationException>(() => release.CompleteRollback(true, false));
            release.CompleteRollback(true, true);
            Assert.That(release.State, Is.EqualTo(ReleaseState.Live));
        }

        [Test]
        public void DeploymentCannotStartWhenCandidateGateIsBlocked()
        {
            var release=new ReleaseController(Build(), new ReleaseValidationEvidence { CandidateBuilt=true, GameplayIntegrityHealthy=false });
            Assert.Throws<InvalidOperationException>(() => release.BeginDeployment());
            Assert.That(release.State, Is.EqualTo(ReleaseState.Candidate));
        }

        [Test]
        public void IncidentLedgerTransitionsAndClearsResolvedReleaseBlocker()
        {
            var ledger=new IncidentLedger();
            ledger.Open(new ProductionIncident("inc-1", IncidentSeverity.P0, IncidentStatus.Open, "score-integrity", DateTime.UtcNow));
            Assert.That(ledger.CountBlocking(), Is.EqualTo(1));
            ledger.Transition("inc-1", IncidentStatus.Investigating);
            ledger.Transition("inc-1", IncidentStatus.Mitigating);
            ledger.Transition("inc-1", IncidentStatus.Resolved);
            Assert.That(ledger.CountBlocking(), Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => ledger.Transition("inc-1", IncidentStatus.Open));
        }

        [Test]
        public void PostDeploymentHealthChecksMustProgressInOrder()
        {
            var checks=new PostDeploymentValidation();
            Assert.Throws<InvalidOperationException>(() => checks.RecordThirtyMinute(true));
            checks.RecordFiveMinute(true);
            checks.RecordThirtyMinute(true);
            checks.RecordOneHour(true);
            Assert.That(checks.Complete, Is.True);
        }
    }
}
