using System.Collections.Generic;

namespace ThiefAndTheNecklace
{
    public sealed class SeededRandomService
    {
        // Small, explicitly specified PRNG so a seed stays reproducible across .NET/Unity versions.
        private uint _state;

        public SeededRandomService(int seed)
        {
            _state = unchecked((uint)seed);
            if (_state == 0) _state = 0x6D2B79F5u;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
            var range = (uint)(maxExclusive - minInclusive);
            var threshold = unchecked(0u - range) % range;
            uint value;
            do { value = NextUInt(); } while (value < threshold);
            return minInclusive + (int)(value % range);
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = Next(0, i + 1);
                var temp = items[i];
                items[i] = items[j];
                items[j] = temp;
            }
        }

        private uint NextUInt()
        {
            var x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }
    }
}
