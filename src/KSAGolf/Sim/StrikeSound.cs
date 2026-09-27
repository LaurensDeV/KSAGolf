namespace KSAGolf;

/// <summary>Which recorded whack a strike or a landing sounds like, and how loud.</summary>
internal static class StrikeSound
{
    public const string Tap = "KSAGolf_PuttTap";
    public const string Soft = "KSAGolf_PuttSoft";
    public const string Hard = "KSAGolf_PuttHard";
    public const string Drop = "KSAGolf_Drop";

    /// <summary>Below this a putt is a tap, and above <see cref="HardFrom"/> a hard hit, in m/s off the face.</summary>
    public const double SoftFrom = 0.6;
    public const double HardFrom = 2.2;

    public static (string Id, float Volume) ForStrike(double launchSpeed)
    {
        if (launchSpeed < SoftFrom) return (Tap, Loudness(launchSpeed, 0.0, SoftFrom, 0.5f));
        if (launchSpeed < HardFrom) return (Soft, Loudness(launchSpeed, SoftFrom, HardFrom, 0.7f));
        return (Hard, Loudness(launchSpeed, HardFrom, 2.0 * HardFrom, 0.8f));
    }

    public static (string Id, float Volume) ForLanding(double impactSpeed)
        => (Drop, Loudness(impactSpeed, GolfBall.AudibleImpact, 3.0, 0.3f));

    // Rises from the floor at the bottom of a band to full at its top.
    private static float Loudness(double speed, double low, double high, float floor)
        => floor + ((1f - floor) * (float)Math.Clamp((speed - low) / (high - low), 0.0, 1.0));
}
