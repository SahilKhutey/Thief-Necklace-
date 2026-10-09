using System;
using System.Collections.Generic;
using System.Linq;
using ThiefAndTheNecklace;

namespace ThiefAndTheNecklace.Networking
{
    public sealed class NetworkChatPolicy
    {
        public const int MaximumLength = 180;
        public const int BurstLimit = 5;
        public const double MinimumIntervalSeconds = 0.25;
        private readonly Dictionary<string, Queue<double>> _sendTimes = new Dictionary<string, Queue<double>>();
        private long _sequence;

        public bool TryAccept(string authenticatedSenderId, string text, RoundPhase phase, double now, out ChatMessage message, out NetworkGameError error)
        {
            message = null;
            error = NetworkGameError.None;
            if (phase != RoundPhase.Discussion) { error = NetworkGameError.InvalidPhase; return false; }
            if (string.IsNullOrWhiteSpace(authenticatedSenderId)) { error = NetworkGameError.NotConnected; return false; }
            var normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0 || normalized.Length > MaximumLength) { error = NetworkGameError.InvalidChat; return false; }
            Queue<double> times;
            if (!_sendTimes.TryGetValue(authenticatedSenderId, out times)) _sendTimes.Add(authenticatedSenderId, times = new Queue<double>());
            while (times.Count > 0 && now - times.Peek() >= 1.0) times.Dequeue();
            if ((times.Count >= BurstLimit) || (times.Count > 0 && now - times.Last() < MinimumIntervalSeconds))
            { error = NetworkGameError.RateLimited; return false; }
            times.Enqueue(now);
            message = new ChatMessage(Guid.NewGuid().ToString("N"), authenticatedSenderId, normalized, ++_sequence);
            return true;
        }

        public void Reset()
        {
            _sendTimes.Clear();
            _sequence = 0;
        }
    }
}
