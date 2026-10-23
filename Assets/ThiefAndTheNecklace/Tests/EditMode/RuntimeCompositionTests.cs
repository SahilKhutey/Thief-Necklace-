using System;
using NUnit.Framework;
using ThiefAndTheNecklace.PB1;

namespace ThiefAndTheNecklace.Tests
{
    public sealed class RuntimeCompositionTests
    {
        private static GameRuntimeConfiguration Configuration() =>
            new GameRuntimeConfiguration(new[] { "A", "B", "C", "D" }, 42, 1, DifficultyLevel.Normal);

        [Test]
        public void CompositionBuildsOneRuntimeAndWiresApplicationAndSafeEvents()
        {
            var root=new GameCompositionRoot();
            var composition=root.Build(Configuration(), new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth()));
            try
            {
                Assert.That(composition.Services.Resolve<CoreGameRuntime>(), Is.SameAs(composition.Gameplay));
                Assert.That(composition.Services.Resolve<IGameApplication>(), Is.TypeOf<CoreGameApplication>());
                var received=0;
                composition.Services.Resolve<IGameEventBus>().Subscribe<GameRuntimeEvent>(evt => received++);
                composition.Services.Resolve<IGameApplication>().StartMatch();
                Assert.That(received, Is.GreaterThan(0));
                Assert.Throws<InvalidOperationException>(() => root.Build(Configuration(), new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth())));
            }
            finally { composition.Dispose(); }
        }

        [Test]
        public void EventBusIsolatesSubscriberExceptionsAndDisposesSubscriptions()
        {
            var bus=new GameEventBus();
            var delivered=0;
            bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("listener failure"));
            var subscription=bus.Subscribe<TestEvent>(_ => delivered++);
            bus.Publish(new TestEvent());
            Assert.That(delivered, Is.EqualTo(1));
            Assert.That(bus.SubscriberFailureCount, Is.EqualTo(1));
            subscription.Dispose();
            bus.Publish(new TestEvent());
            Assert.That(delivered, Is.EqualTo(1));
            bus.Dispose();
        }

        [Test]
        public void CompositionUsesInjectedClockAndPhaseGateRejectsInvalidFlow()
        {
            var clock=new ManualGameClock(1234);
            var composition=new GameCompositionRoot().Build(Configuration(),
                new StaticScenarioGenerator(DefaultScenarioFactory.CreateRoyalNecklaceTruth()), clock, new SeededRandomSource(42));
            try
            {
                Assert.That(composition.Services.Resolve<IGameClock>(), Is.SameAs(clock));
                composition.Services.Resolve<GameFlowCoordinator>().Start();
                GameplayPhaseGate.Require(composition.Gameplay, RoundPhase.PrivateReveal);
                Assert.Throws<InvalidOperationException>(() => GameplayPhaseGate.Require(composition.Gameplay, RoundPhase.Voting));
                composition.Services.Resolve<GameFlowCoordinator>().BeginDiscussion();
                Assert.That(composition.Gameplay.GetDiscussionSnapshot().StartTime, Is.EqualTo(1234));
                clock.Advance(100);
                Assert.That(clock.UtcNowMilliseconds, Is.EqualTo(1334));
            }
            finally { composition.Dispose(); }
        }

        [Test]
        public void OperationScopeCancelsOwnedWorkInReverseOrderAndContinuesAfterFailure()
        {
            var scope=new OperationScope();
            var order=new System.Collections.Generic.List<int>();
            scope.Own(new FakeOperation(() => order.Add(1)));
            scope.Own(new FakeOperation(() => { order.Add(2); throw new InvalidOperationException(); }));
            scope.Own(new FakeOperation(() => order.Add(3)));
            scope.Dispose();
            Assert.That(order, Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(scope.CancellationFailureCount, Is.EqualTo(1));
        }

        [Test]
        public void ServiceRegistryDisposesOwnedInstancesButLeavesBorrowedServicesAlive()
        {
            var registry=new ServiceRegistry();
            var owned=new DisposableService();
            var borrowed=new BorrowedService();
            registry.Register<IDisposable>(owned);
            registry.RegisterBorrowed<IBorrowedService>(borrowed);
            registry.Dispose();
            Assert.That(owned.DisposeCount, Is.EqualTo(1));
            Assert.That(borrowed.DisposeCount, Is.EqualTo(0));
        }

        private sealed class TestEvent : IGameEvent { }
        private sealed class FakeOperation : ICancelableOperation
        {
            private readonly Action cancel;
            public FakeOperation(Action cancel) { this.cancel=cancel; }
            public void Cancel() => cancel();
        }
        private sealed class DisposableService : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() { DisposeCount++; }
        }
        private interface IBorrowedService { }
        private sealed class BorrowedService : IBorrowedService, IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() { DisposeCount++; }
        }
    }
}
