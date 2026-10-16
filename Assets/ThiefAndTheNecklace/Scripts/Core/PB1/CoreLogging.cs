using System;

namespace ThiefAndTheNecklace.PB1
{
    public enum CoreLogLevel { Trace, Debug, Info, Warn, Error, Fatal }

    [Serializable]
    public sealed class CoreLogEvent
    {
        public DateTime TimestampUtc;
        public CoreLogLevel Level;
        public string EventType = string.Empty;
        public string MatchId = string.Empty;
        public int Round;
        public string Message = string.Empty;

        public CoreLogEvent DeepCopy() => new CoreLogEvent
        {
            TimestampUtc = TimestampUtc,
            Level = Level,
            EventType = EventType,
            MatchId = MatchId,
            Round = Round,
            Message = Message
        };
    }
}
