namespace ThiefAndTheNecklace.Visual
{
    /// <summary>Components request semantic motion; settings decide how a profile is presented.</summary>
    public enum VisualMotionProfile { Fast, Normal, Slow, Cinematic, CountUp, Pulse, Shake, CardFlip }
    public enum VisualMotionPreference { FullMotion, ReducedMotion, NoMotion }

    public static class VisualMotion
    {
        public static int DurationMilliseconds(VisualMotionProfile profile, VisualMotionPreference preference)
        {
            if (preference == VisualMotionPreference.NoMotion) return 0;
            if (preference == VisualMotionPreference.ReducedMotion) return 140;

            switch (profile)
            {
                case VisualMotionProfile.Fast: return 100;
                case VisualMotionProfile.Normal: return 180;
                case VisualMotionProfile.Slow: return 300;
                case VisualMotionProfile.Cinematic: return 650;
                case VisualMotionProfile.CountUp: return 420;
                case VisualMotionProfile.Pulse: return 180;
                case VisualMotionProfile.Shake: return 280;
                case VisualMotionProfile.CardFlip: return 420;
                default: return VisualDesignTokens.MotionNormalMilliseconds;
            }
        }
    }
}
