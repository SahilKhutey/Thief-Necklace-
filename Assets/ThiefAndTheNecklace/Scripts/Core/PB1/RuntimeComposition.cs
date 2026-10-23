using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.PB1
{
    public interface IGameClock { long UtcNowMilliseconds { get; } }
    public sealed class SystemGameClock : IGameClock
    { public long UtcNowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

    /// <summary>Manually advanced test clock; production uses SystemGameClock.</summary>
    public sealed class ManualGameClock : IGameClock
    {
        public long UtcNowMilliseconds { get; private set; }
        public ManualGameClock(long initialMilliseconds = 0) { UtcNowMilliseconds=initialMilliseconds; }
        public void Advance(long milliseconds) { if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds)); UtcNowMilliseconds=checked(UtcNowMilliseconds + milliseconds); }
    }

    public interface IRandomSource { int Next(int exclusiveMaximum); }
    public sealed class SeededRandomSource : IRandomSource
    {
        private uint state;
        public SeededRandomSource(int seed) { state=unchecked((uint)seed); if (state == 0) state=0x9E3779B9u; }
        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)exclusiveMaximum);
        }
    }

    public interface ICancelableOperation { void Cancel(); }
    public interface IScreenRouter { void Show(string screenId); void Hide(string screenId); }
    public interface IRuntimeAssetLease<out TAsset> : IDisposable { TAsset Asset { get; } }
    /// <summary>Unity Addressables or another asset host implements this; consumers dispose the returned lease.</summary>
    public interface IRuntimeAssetProvider<TAsset> { System.Threading.Tasks.Task<IRuntimeAssetLease<TAsset>> LoadAsync(string assetKey, System.Threading.CancellationToken cancellationToken); }
    public interface IObjectPool<TObject> where TObject : class { TObject Rent(); void Return(TObject instance); }
    public interface IGameCompositionModule { void Register(IServiceRegistry services); }

    public sealed class RuntimeError
    {
        public string CorrelationId { get; }
        public string ErrorCode { get; }
        public string ExceptionType { get; }
        public RuntimeError(string correlationId, string errorCode, Exception exception)
        { CorrelationId=correlationId ?? string.Empty; ErrorCode=errorCode ?? string.Empty; ExceptionType=exception?.GetType().Name ?? string.Empty; }
    }

    /// <summary>Maps an exception to safe diagnostics without exposing exception messages or private gameplay payloads.</summary>
    public sealed class RuntimeErrorBoundary
    {
        private readonly Action<RuntimeError> report;
        public RuntimeErrorBoundary(Action<RuntimeError> report) { this.report=report ?? (_ => { }); }
        public bool TryExecute(string errorCode, Action operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            try { operation(); return true; }
            catch (Exception exception)
            {
                var correlationId=Guid.NewGuid().ToString("N");
                try { report(new RuntimeError(correlationId, errorCode, exception)); } catch { }
                return false;
            }
        }
    }

    /// <summary>Owns transient async work and cancels it in reverse registration order on scene/session teardown.</summary>
    public sealed class OperationScope : IDisposable
    {
        private readonly object gate=new object();
        private readonly List<ICancelableOperation> operations=new List<ICancelableOperation>();
        private bool disposed;
        public int CancellationFailureCount { get; private set; }
        public void Own(ICancelableOperation operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            var cancelImmediately=false;
            lock (gate) { if (disposed) cancelImmediately=true; else operations.Add(operation); }
            if (cancelImmediately) CancelSafely(operation);
        }
        public void Dispose()
        {
            ICancelableOperation[] pending;
            lock (gate) { if (disposed) return; disposed=true; pending=operations.ToArray(); operations.Clear(); }
            for (var i=pending.Length - 1; i >= 0; i--) CancelSafely(pending[i]);
        }
        private void CancelSafely(ICancelableOperation operation) { try { operation.Cancel(); } catch { lock (gate) CancellationFailureCount++; } }
    }

    public static class GameplayPhaseGate
    {
        public static void Require(CoreGameRuntime runtime, RoundPhase expected)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (runtime.State.Match.Status != MatchStatus.InProgress || runtime.State.Match.CurrentRoundState.Phase != expected)
                throw new InvalidOperationException("Gameplay is not ready for phase " + expected + ".");
        }
    }

    public interface IServiceRegistry : IDisposable
    {
        void Register<TService>(TService service) where TService : class;
        void RegisterBorrowed<TService>(TService service) where TService : class;
        TService Resolve<TService>() where TService : class;
        bool TryResolve<TService>(out TService service) where TService : class;
    }

    /// <summary>Explicit, per-composition service ownership; no global locator or implicit construction.</summary>
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly object gate=new object();
        private readonly Dictionary<Type, object> services=new Dictionary<Type, object>();
        private readonly HashSet<object> owned=new HashSet<object>(ReferenceEqualityComparer.Instance);
        private readonly List<object> ownedOrder=new List<object>();
        private bool disposed;

        public void Register<TService>(TService service) where TService : class
            => Register(service, true);

        public void RegisterBorrowed<TService>(TService service) where TService : class
            => Register(service, false);

        private void Register<TService>(TService service, bool takeOwnership) where TService : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            lock (gate)
            {
                ThrowIfDisposed();
                var type=typeof(TService);
                if (services.ContainsKey(type)) throw new InvalidOperationException("Service is already registered: " + type.FullName);
                if (!type.IsInstanceOfType(service)) throw new ArgumentException("Registered instance does not implement its service type.", nameof(service));
                services.Add(type, service);
                if (takeOwnership && owned.Add(service)) ownedOrder.Add(service);
            }
        }

        public TService Resolve<TService>() where TService : class
        {
            TService service;
            if (TryResolve(out service)) return service;
            throw new InvalidOperationException("Service is not registered: " + typeof(TService).FullName);
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            lock (gate)
            {
                ThrowIfDisposed();
                object instance;
                if (services.TryGetValue(typeof(TService), out instance)) { service=(TService)instance; return true; }
                service=null; return false;
            }
        }

        public void Dispose()
        {
            object[] owned;
            lock (gate)
            {
                if (disposed) return;
                disposed=true;
                owned=ownedOrder.AsEnumerable().Reverse().ToArray();
                this.owned.Clear();
                ownedOrder.Clear();
                services.Clear();
            }
            foreach (var item in owned)
            {
                var disposable=item as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }

        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ServiceRegistry)); }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance=new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
    }

    public interface IGameEvent { }
    public interface IGameEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
        void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent;
    }

    /// <summary>Typed in-process event routing. Subscriber failures do not stop authoritative gameplay or other listeners.</summary>
    public sealed class GameEventBus : IGameEventBus, IDisposable
    {
        private readonly object gate=new object();
        private readonly Dictionary<Type, List<Delegate>> subscribers=new Dictionary<Type, List<Delegate>>();
        private bool disposed;
        public int SubscriberFailureCount { get; private set; }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(GameEventBus));
                var type=typeof(TEvent); List<Delegate> list;
                if (!subscribers.TryGetValue(type, out list)) { list=new List<Delegate>(); subscribers.Add(type, list); }
                list.Add(handler);
            }
            return new Subscription(() => Remove(typeof(TEvent), handler));
        }

        public void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent
        {
            Delegate[] handlers;
            lock (gate)
            {
                if (disposed) return;
                List<Delegate> list;
                handlers=subscribers.TryGetValue(typeof(TEvent), out list) ? list.ToArray() : Array.Empty<Delegate>();
            }
            foreach (var handler in handlers)
            {
                try { ((Action<TEvent>)handler)(evt); }
                catch { lock (gate) SubscriberFailureCount++; }
            }
        }

        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed=true; subscribers.Clear(); }
        }

        private void Remove(Type type, Delegate handler)
        {
            lock (gate)
            {
                if (disposed) return;
                List<Delegate> list;
                if (subscribers.TryGetValue(type, out list)) { list.Remove(handler); if (list.Count == 0) subscribers.Remove(type); }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;
            public Subscription(Action dispose) { this.dispose=dispose; }
            public void Dispose() { var callback=System.Threading.Interlocked.Exchange(ref dispose, null); callback?.Invoke(); }
        }
    }

    public sealed class GameRuntimeEvent : IGameEvent
    {
        public long Sequence { get; }
        public DateTime TimestampUtc { get; }
        public string MatchId { get; }
        public int Round { get; }
        public string EventType { get; }
        public GameRuntimeEvent(long sequence, DateTime timestampUtc, string matchId, int round, string eventType)
        { Sequence=sequence; TimestampUtc=timestampUtc; MatchId=matchId ?? string.Empty; Round=round; EventType=eventType ?? string.Empty; }
    }

    /// <summary>Bridges safe core log metadata to the app event bus without forwarding private event payloads.</summary>
    public sealed class CoreGameplayEventBridge : IDisposable
    {
        private readonly CoreGameRuntime runtime;
        private readonly IGameEventBus eventBus;
        private long sequence;
        private bool disposed;
        public CoreGameplayEventBridge(CoreGameRuntime runtime, IGameEventBus eventBus)
        {
            this.runtime=runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.eventBus=eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            runtime.CoreEventLogged += OnCoreEvent;
        }
        private void OnCoreEvent(CoreLogEvent item)
        {
            if (disposed || item == null) return;
            eventBus.Publish(new GameRuntimeEvent(++sequence, item.TimestampUtc, item.MatchId, item.Round, item.EventType));
        }
        public void Dispose()
        {
            if (disposed) return; disposed=true; runtime.CoreEventLogged -= OnCoreEvent;
        }
    }

    public interface IGameApplication
    {
        void StartMatch();
        void StartDiscussion();
        void StartVoting();
        VoteResolution ResolveVotes();
        void Reveal();
        void Score();
        void CompleteRound();
        MatchResult CompleteMatch();
    }

    /// <summary>Application facade coordinates existing authoritative domain commands without implementing their rules.</summary>
    public sealed class CoreGameApplication : IGameApplication
    {
        private readonly CoreGameRuntime runtime;
        public CoreGameApplication(CoreGameRuntime runtime) { this.runtime=runtime ?? throw new ArgumentNullException(nameof(runtime)); }
        public void StartMatch() => runtime.StartMatch();
        public void StartDiscussion() => runtime.BeginDiscussion();
        public void StartVoting() => runtime.BeginVoting();
        public VoteResolution ResolveVotes() => runtime.ResolveVote();
        public void Reveal() => runtime.CompleteReveal();
        public void Score() => runtime.ResolveScoring();
        public void CompleteRound() => runtime.FinishRound();
        public MatchResult CompleteMatch()
        {
            if (runtime.State.Match.Status != MatchStatus.Completed) throw new InvalidOperationException("The final round must complete before a match result is available.");
            var result=runtime.GetMatchResult();
            if (result == null) throw new InvalidOperationException("A completed match must have a finalized result.");
            return result;
        }
    }

    /// <summary>Application-level lifecycle coordinator; phase rules remain enforced by the PB1 runtime.</summary>
    public sealed class GameFlowCoordinator
    {
        private readonly CoreGameRuntime runtime;
        private readonly IGameApplication application;
        public GameFlowCoordinator(CoreGameRuntime runtime, IGameApplication application)
        { this.runtime=runtime ?? throw new ArgumentNullException(nameof(runtime)); this.application=application ?? throw new ArgumentNullException(nameof(application)); }
        public void Start() { application.StartMatch(); GameplayPhaseGate.Require(runtime, RoundPhase.PrivateReveal); }
        public void BeginDiscussion() { GameplayPhaseGate.Require(runtime, RoundPhase.PrivateReveal); application.StartDiscussion(); }
        public void BeginVoting() { GameplayPhaseGate.Require(runtime, RoundPhase.Discussion); application.StartVoting(); }
        public void ResolveVote() { GameplayPhaseGate.Require(runtime, RoundPhase.Voting); application.ResolveVotes(); }
        public void Reveal() { GameplayPhaseGate.Require(runtime, RoundPhase.Reveal); application.Reveal(); }
        public void Score() { GameplayPhaseGate.Require(runtime, RoundPhase.Reveal); application.Score(); }
        public void CompleteRound() { GameplayPhaseGate.Require(runtime, RoundPhase.Results); application.CompleteRound(); }
    }

    public sealed class GameRuntimeConfiguration
    {
        public IReadOnlyList<string> PlayerNames { get; }
        public int Seed { get; }
        public int TotalRounds { get; }
        public DifficultyLevel Difficulty { get; }
        public bool RequirePrivateInformationAcknowledgement { get; }
        public GameRuntimeConfiguration(IEnumerable<string> playerNames, int seed, int totalRounds,
            DifficultyLevel difficulty, bool requirePrivateInformationAcknowledgement = false)
        {
            var copy=(playerNames ?? Enumerable.Empty<string>()).ToArray();
            if (copy.Length < 4 || copy.Length > 6) throw new ArgumentException("Gameplay runtime currently supports 4–6 players.", nameof(playerNames));
            if (copy.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Player names must not be empty.", nameof(playerNames));
            if (copy.Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length) throw new ArgumentException("Player names must be unique within the match.", nameof(playerNames));
            if (totalRounds < 1) throw new ArgumentOutOfRangeException(nameof(totalRounds));
            if (!Enum.IsDefined(typeof(DifficultyLevel), difficulty)) throw new ArgumentOutOfRangeException(nameof(difficulty));
            PlayerNames=Array.AsReadOnly(copy); Seed=seed; TotalRounds=totalRounds; Difficulty=difficulty;
            RequirePrivateInformationAcknowledgement=requirePrivateInformationAcknowledgement;
        }
        public bool RequirePrivateInformationAcknowledgement { get; }
    }

    public sealed class GameComposition : IDisposable
    {
        public IServiceRegistry Services { get; }
        public CoreGameRuntime Gameplay { get; }
        private readonly CoreGameplayEventBridge eventBridge;
        public OperationScope Operations { get; }
        private bool disposed;
        internal GameComposition(IServiceRegistry services, CoreGameRuntime gameplay, CoreGameplayEventBridge eventBridge, OperationScope operations)
        { Services=services; Gameplay=gameplay; this.eventBridge=eventBridge; Operations=operations; }
        public void Dispose()
        {
            if (disposed) return;
            disposed=true;
            Operations.Dispose();
            eventBridge.Dispose();
            Services.Dispose();
        }
    }

    /// <summary>Single owner that assembles the engine-independent gameplay runtime and app event pipeline.</summary>
    public sealed class GameCompositionRoot
    {
        private bool built;
        public GameComposition Build(GameRuntimeConfiguration configuration, IScenarioGenerator scenarioGenerator,
            IGameClock clock = null, IRandomSource random = null, IEnumerable<IGameCompositionModule> modules = null)
        {
            if (built) throw new InvalidOperationException("A composition root builds exactly one runtime instance.");
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (scenarioGenerator == null) throw new ArgumentNullException(nameof(scenarioGenerator));
            var registry=new ServiceRegistry();
            try
            {
                var runtimeClock=clock ?? new SystemGameClock();
                var runtimeRandom=random ?? new SeededRandomSource(configuration.Seed);
                var events=new GameEventBus();
                var runtime=new CoreGameRuntime(configuration.PlayerNames, configuration.Seed, configuration.TotalRounds,
                    configuration.Difficulty, scenarioGenerator, configuration.RequirePrivateInformationAcknowledgement,
                    timestampProvider: () => runtimeClock.UtcNowMilliseconds);
                var bridge=new CoreGameplayEventBridge(runtime, events);
                var operations=new OperationScope();
                registry.Register<IGameEventBus>(events);
                registry.Register<CoreGameRuntime>(runtime);
                registry.Register<IGameApplication>(new CoreGameApplication(runtime));
                registry.Register<GameFlowCoordinator>(new GameFlowCoordinator(runtime, registry.Resolve<IGameApplication>()));
                registry.Register<CoreGameplayEventBridge>(bridge);
                registry.Register<IGameClock>(runtimeClock);
                registry.Register<IRandomSource>(runtimeRandom);
                registry.Register<OperationScope>(operations);
                foreach (var module in modules ?? Enumerable.Empty<IGameCompositionModule>())
                {
                    if (module == null) throw new ArgumentException("Composition modules cannot contain null entries.", nameof(modules));
                    module.Register(registry);
                }
                built=true;
                return new GameComposition(registry, runtime, bridge, operations);
            }
            catch { registry.Dispose(); throw; }
        }
    }
}
