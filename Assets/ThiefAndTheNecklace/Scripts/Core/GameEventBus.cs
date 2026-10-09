using System;
using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    /// <summary>In-process presentation event bus. Events are notifications, never gameplay authority.</summary>
    public sealed class GameEventBus
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, List<Subscription>> _subscriptions = new Dictionary<Type, List<Subscription>>();

        public IDisposable Subscribe<T>(Action<T> handler) where T : class
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var subscription = new Subscription(this, typeof(T), value => handler((T)value));
            lock (_gate)
            {
                if (!_subscriptions.TryGetValue(typeof(T), out var list))
                    _subscriptions.Add(typeof(T), list = new List<Subscription>());
                list.Add(subscription);
            }
            return subscription;
        }

        public void Publish<T>(T gameEvent) where T : class
        {
            if (gameEvent == null) throw new ArgumentNullException(nameof(gameEvent));
            Subscription[] listeners;
            lock (_gate)
                listeners = _subscriptions.TryGetValue(typeof(T), out var list) ? list.ToArray() : new Subscription[0];
            foreach (var listener in listeners) listener.Invoke(gameEvent);
        }

        private void Remove(Subscription subscription)
        {
            lock (_gate)
            {
                if (_subscriptions.TryGetValue(subscription.EventType, out var list))
                {
                    list.Remove(subscription);
                    if (list.Count == 0) _subscriptions.Remove(subscription.EventType);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private GameEventBus _owner;
            private readonly Action<object> _handler;
            public Type EventType { get; }
            public Subscription(GameEventBus owner, Type eventType, Action<object> handler)
            { _owner = owner; EventType = eventType; _handler = handler; }
            public void Invoke(object value) { if (_owner != null) _handler(value); }
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                if (owner != null) owner.Remove(this);
            }
        }
    }

    public sealed class RoundPhaseChangedEvent
    {
        public readonly RoundPhase Phase;
        public readonly int RoundNumber;
        public RoundPhaseChangedEvent(RoundPhase phase, int roundNumber) { Phase = phase; RoundNumber = roundNumber; }
    }

    public sealed class VoteFeedbackEvent
    {
        public readonly bool Locked;
        public VoteFeedbackEvent(bool locked) { Locked = locked; }
    }

    public sealed class RevealFeedbackEvent
    {
        public readonly bool ThiefCaught;
        public RevealFeedbackEvent(bool thiefCaught) { ThiefCaught = thiefCaught; }
    }

    public sealed class ScoreFeedbackEvent
    {
        public readonly int TotalPointsAwarded;
        public ScoreFeedbackEvent(int totalPointsAwarded) { TotalPointsAwarded = totalPointsAwarded; }
    }
}
