using System;
using System.Collections.Generic;
using System.Linq;

namespace ThiefAndTheNecklace.Backend
{
    /// <summary>Process-local guardrail. Production APIs must also enforce limits server-side.</summary>
    public sealed class FixedWindowRateLimiter
    {
        private sealed class Window { public long StartedAt; public int Count; }
        private readonly Dictionary<string, Window> _windows = new Dictionary<string, Window>(StringComparer.Ordinal);
        private readonly int _limit;
        private readonly long _windowTicks;
        public FixedWindowRateLimiter(int limit, TimeSpan window)
        { if (limit < 1 || window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(); _limit=limit; _windowTicks=window.Ticks; }
        public bool TryConsume(string key, long utcTicks)
        {
            if (string.IsNullOrWhiteSpace(key) || utcTicks <= 0) return false;
            if (!_windows.TryGetValue(key, out var window) || utcTicks - window.StartedAt >= _windowTicks || utcTicks < window.StartedAt)
                _windows[key] = window = new Window { StartedAt=utcTicks, Count=0 };
            if (window.Count >= _limit) return false;
            window.Count++; return true;
        }
    }

    public static class PlayerInputValidator
    {
        public static bool TryNormalizeDisplayName(string input, out string normalized)
        {
            normalized = (input ?? string.Empty).Trim();
            if (normalized.Length < 2 || normalized.Length > 20 || normalized.Any(char.IsControl)) return false;
            if (normalized.Any(c => !(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) || c == '_' || c == '-'))) return false;
            return true;
        }

        public static bool IsBoundedText(string value, int maximumLength, bool allowEmpty = false) =>
            value != null && value.Length <= maximumLength && (allowEmpty || value.Length > 0) && !value.Any(c => c == '\0');

        public static bool IsValidReport(PlayerReport report, string authenticatedPlayerId) => report != null &&
            !string.IsNullOrWhiteSpace(report.reportId) && report.reportId.Length <= 80 &&
            !string.IsNullOrWhiteSpace(authenticatedPlayerId) && report.reporterPlayerId == authenticatedPlayerId &&
            !string.IsNullOrWhiteSpace(report.targetPlayerId) && report.targetPlayerId != authenticatedPlayerId &&
            Enum.IsDefined(typeof(PlayerReportCategory), report.category);
    }

    public enum CircuitState { Closed, Open, HalfOpen }
    /// <summary>Provider-neutral circuit state helper; callers choose retry delays and the monotonic time source.</summary>
    public sealed class CircuitBreaker
    {
        private readonly int _failureThreshold;
        private readonly long _resetAfterTicks;
        private int _failures;
        private long _openedAt;
        private bool _probeInFlight;
        public CircuitState State { get; private set; } = CircuitState.Closed;
        public CircuitBreaker(int failureThreshold, TimeSpan resetAfter)
        { if (failureThreshold < 1 || resetAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(); _failureThreshold=failureThreshold; _resetAfterTicks=resetAfter.Ticks; }
        public bool TryEnter(long monotonicTicks)
        {
            if (State == CircuitState.Closed) return true;
            if (State == CircuitState.Open && monotonicTicks - _openedAt >= _resetAfterTicks) { State=CircuitState.HalfOpen; _probeInFlight=false; }
            if (State != CircuitState.HalfOpen || _probeInFlight) return false;
            _probeInFlight=true; return true;
        }
        public void RecordSuccess() { _failures=0; _probeInFlight=false; State=CircuitState.Closed; }
        public void RecordFailure(long monotonicTicks)
        {
            _probeInFlight=false;
            if (State == CircuitState.HalfOpen || ++_failures >= _failureThreshold) { State=CircuitState.Open; _openedAt=monotonicTicks; }
        }
    }
}
