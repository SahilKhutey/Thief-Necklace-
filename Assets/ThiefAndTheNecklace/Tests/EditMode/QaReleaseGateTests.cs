using System;
using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class QaReleaseGateTests
    {
        private static ReleaseSignoff ApprovedSignoff(string build = "1.0.0") =>
            new ReleaseSignoff(build, true, true, true, true, true, true, true);

        private static QaTestRun PassedRun(string build = "1.0.0") =>
            new QaTestRun(build, "Windows", DateTimeOffset.UnixEpoch,
                new[] { new QaTestResult("GW14-SMOKE-001", QaTestStatus.Passed, 12) });

        [Test]
        public void ReleaseGateApprovesOnlyMatchingValidatedBuildWithCompleteMatrixAndSignoff()
        {
            var evaluator = new ReleaseGateEvaluator();
            var approved = evaluator.Evaluate(new ReleaseGateInput("1.0.0", PassedRun(), 0, 0,
                ApprovedSignoff(), true, true));
            Assert.That(approved.Approved, Is.True);
            Assert.That(approved.Blockers, Is.Empty);

            var incompletePlayers = evaluator.Evaluate(new ReleaseGateInput("1.0.0", PassedRun(), 0, 0,
                ApprovedSignoff(), true, false));
            Assert.That(incompletePlayers.Approved, Is.False);
            Assert.That(incompletePlayers.Blockers.Any(b => b.Contains("player-count")), Is.True);
        }

        [TestCase(QaTestStatus.Failed)]
        [TestCase(QaTestStatus.Blocked)]
        [TestCase(QaTestStatus.Skipped)]
        [TestCase(QaTestStatus.NotRun)]
        public void NonPassingResultBlocksRelease(QaTestStatus status)
        {
            var failure = status == QaTestStatus.Failed ? "Observed failure" : null;
            var run = new QaTestRun("1.0.0", "Windows", DateTimeOffset.UnixEpoch,
                new[] { new QaTestResult("GW14-CASE-001", status, 0, failure) });
            Assert.That(run.Passed, Is.False);
            var decision = new ReleaseGateEvaluator().Evaluate(new ReleaseGateInput("1.0.0", run, 0, 0,
                ApprovedSignoff(), true, true));
            Assert.That(decision.Approved, Is.False);
        }

        [Test]
        public void ReleaseGateRejectsP0P1BuildMismatchAndMissingSignoff()
        {
            var decision = new ReleaseGateEvaluator().Evaluate(new ReleaseGateInput("2.0.0", PassedRun("1.0.0"),
                1, 1, ApprovedSignoff("1.0.0"), false, true));
            Assert.That(decision.Approved, Is.False);
            Assert.That(decision.Blockers.Count, Is.GreaterThanOrEqualTo(5));

            var missingSignoff = new ReleaseGateEvaluator().Evaluate(new ReleaseGateInput("1.0.0", PassedRun(), 0, 0,
                null, true, true));
            Assert.That(missingSignoff.Blockers.Any(b => b.Contains("sign-off")), Is.True);
        }

        [Test]
        public void DefectLifecycleRequiresReplayAndTraceToReopenVerifiedIssue()
        {
            var defect = new DefectRecord("BUG-42", DefectSeverity.P0Critical);
            defect.TransitionTo(DefectStatus.Triaged);
            defect.TransitionTo(DefectStatus.Assigned);
            defect.TransitionTo(DefectStatus.Fixed);
            defect.TransitionTo(DefectStatus.Retest);
            defect.TransitionTo(DefectStatus.Verified);
            Assert.Throws<InvalidOperationException>(() => defect.TransitionTo(DefectStatus.Reopened));
            defect.TransitionTo(DefectStatus.Reopened, "replay-7", "trace-7");
            Assert.That(defect.Status, Is.EqualTo(DefectStatus.Reopened));
            Assert.That(defect.ReplayId, Is.EqualTo("replay-7"));
            Assert.That(defect.TraceId, Is.EqualTo("trace-7"));
        }

        [Test]
        public void TestCaseAndRunOwnDetachedCollectionsAndRejectDuplicateIds()
        {
            var steps = new[] { "Open vote", "Submit a target" };
            var testCase = new QaTestCase("GW14-VOTE-001", "Reject self vote", "Voting phase", steps,
                "No vote recorded", DefectSeverity.P0Critical,
                new[] { QaTestCategory.Security, QaTestCategory.Regression });
            steps[0] = "mutated";
            Assert.That(testCase.Steps[0], Is.EqualTo("Open vote"));

            Assert.Throws<ArgumentException>(() => new QaTestRun("1", "Windows", DateTimeOffset.UnixEpoch,
                new[] { new QaTestResult("same", QaTestStatus.Passed, 1), new QaTestResult("same", QaTestStatus.Passed, 2) }));
        }
    }
}
