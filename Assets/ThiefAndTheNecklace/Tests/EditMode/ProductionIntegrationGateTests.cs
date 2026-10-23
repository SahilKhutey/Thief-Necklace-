using System.Linq;
using NUnit.Framework;
using ThiefAndTheNecklace.Backend;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class ProductionIntegrationGateTests
    {
        private static ProductionBuildTarget[] Targets() =>
            (ProductionBuildTarget[])System.Enum.GetValues(typeof(ProductionBuildTarget));

        private static ProductionIntegrationManifest Manifest(int maxPlayers = 6, string appId = "test-project-id") =>
            new ProductionIntegrationManifest(
                new EnvironmentConfig(EnvironmentType.Production, "production", true, true),
                new ClientVersionInfo("1.0.0", 1, 1, 1), appId, 4, maxPlayers,
                false, false, false, Targets());

        [Test]
        public void ProductionGateRejectsPlaceholderIdentityAndUnboundProviderAdapters()
        {
            var report=ProductionIntegrationGate.Validate(Manifest(appId: "YOUR_APP_ID"),
                new ProductionServiceBindings(null, null, null, null, null));
            Assert.That(report.IsReady, Is.False);
            Assert.That(report.Errors.Any(error => error.Contains("application ID")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("Network transport")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("Authenticated-player")), Is.True);
            Assert.Throws<System.InvalidOperationException>(() => ProductionRuntimeServices.Compose(
                Manifest(appId: "YOUR_APP_ID"), new ProductionServiceBindings(null, null, null, null, null)));
        }

        [Test]
        public void ProductionGateRejectsDebugTogglesAndPlayerCountsOutsideImplementedRange()
        {
            var eightPlayers=new ProductionIntegrationManifest(
                new EnvironmentConfig(EnvironmentType.Production, "production", true, true),
                new ClientVersionInfo("1.0.0", 1, 1, 1), "real-app-id", 4, 8,
                true, true, true, Targets());
            var report=ProductionIntegrationGate.Validate(eightPlayers, null);
            Assert.That(report.Errors.Any(error => error.Contains("4–6 players")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("Debug mutation")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("service bindings")), Is.True);
        }

        [Test]
        public void ProductionGateRequiresEveryDeclaredQaAndReleaseTarget()
        {
            var manifest=new ProductionIntegrationManifest(
                new EnvironmentConfig(EnvironmentType.Production, "production", false, false),
                new ClientVersionInfo("1.0.0", 1, 1, 1), "real-app-id", 4, 6,
                false, false, false, new[] { ProductionBuildTarget.WindowsQa });
            var report=ProductionIntegrationGate.Validate(manifest, null);
            Assert.That(report.Errors.Any(error => error.Contains("AndroidRelease")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("IosRelease")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("WebGlRelease")), Is.True);
        }

        [Test]
        public void RuntimeHealthAndMatchReadinessRequireAllMandatoryInputs()
        {
            var health=RuntimeHealthGate.Check(new[] { new RuntimeServiceHealth("network", BackendHealthState.Healthy) }, new[] { "network", "profiles" });
            Assert.That(health.IsHealthy, Is.False);
            Assert.That(health.Errors.Any(error => error.Contains("profiles")), Is.True);
            var errors=MatchReadinessGate.Validate(new MatchReadinessInput
            {
                NetworkConnected=true, RoomValid=true, ConfigurationValid=true, AuthorityAvailable=true,
                PlayerCount=8, ServiceHealth=health
            });
            Assert.That(errors.Any(error => error.Contains("4–6 players")), Is.True);
            Assert.That(errors.Any(error => error.Contains("profiles")), Is.True);
        }

        [Test]
        public void BuildMetadataRejectsIncompleteReleaseProvenance()
        {
            var errors=new BuildMetadata("app-id", "1.0", 0, 1, 1, 1, "", "Android").Validate();
            Assert.That(errors.Any(error => error.Contains("Build number")), Is.True);
            Assert.That(errors.Any(error => error.Contains("Source revision")), Is.True);
        }

        [Test]
        public void ProductionManifestRequiresBuildIdentityAndFrozenConfiguration()
        {
            var report=ProductionIntegrationGate.Validate(Manifest(appId: "real-app-id"), null);
            Assert.That(report.Errors, Does.Contain("Unique build identity and source revision metadata are missing."));
            Assert.That(report.Errors, Does.Contain("A frozen Production configuration snapshot is missing."));
        }
    }
}
