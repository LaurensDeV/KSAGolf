namespace KSAGolf;

/// <summary>
/// Decides what to do with the simulation step KSA has just applied.
///
/// <para><b>Never player time.</b> That is wall-clock: it keeps running while the game is paused,
/// so anything timed on it acts in a frozen world, and it ignores timewarp, so the world outruns
/// whatever the mod integrates.</para>
///
/// <para><b>And it must be the step KSA applied, not one the mod measures.</b> Differencing a
/// clock from a postfix hook can land a step out of phase, and a moving body then integrates over a
/// different span than the world moved by — multiplied by ~29.8 km/s, a sub-millisecond wobble is
/// tens of metres of alternating lateral error. <c>Universe.GetLastSimStep().DeltaTime</c> is the
/// applied step and cannot be out of phase with itself.</para>
///
/// <para>Stateless on purpose: with no differencing there is no previous sample to hold, and
/// therefore no priming, no reset, and no way to be wrong across a scene change.</para>
/// </summary>
internal static class SimClock
{
    /// <summary>What the caller should do with this frame.</summary>
    internal enum State
    {
        /// <summary>Paused, or no simulated time passed. Do nothing at all.</summary>
        Idle,

        /// <summary>Step the simulation by the reported delta.</summary>
        Run,

        /// <summary>
        /// More time passed than can be integrated faithfully. Abandon whatever is in flight
        /// rather than stepping.
        /// </summary>
        Skipped,
    }

    /// <summary>
    /// Largest step integrated when the caller states none: a third of a second, about 19x warp at
    /// 60 fps. A default, not a limit -- anything that can integrate longer steps passes its own.
    /// </summary>
    public const double MaxStep = 0.32;

    /// <summary>
    /// Classifies the step KSA has just applied.
    /// </summary>
    /// <param name="stepSeconds">Simulated seconds the world just advanced by.</param>
    /// <param name="paused">Whether the game is paused.</param>
    /// <param name="dt">Seconds to advance by; zero unless the result is <see cref="State.Run"/>.</param>
    public static State Classify(double stepSeconds, bool paused, out double dt,
                                 double maxStep = MaxStep)
    {
        dt = 0.0;

        // Pause is checked as well as the step, not instead of it: a paused game reports no
        // step anyway; saying so explicitly keeps acting-while-paused unreachable.
        if (paused) return State.Idle;

        // A non-finite or negative step is not something to reason about. Neither is zero.
        if (!double.IsFinite(stepSeconds) || stepSeconds <= 0.0) return State.Idle;

        // Past this a clamped sub-step count starts stretching each sub-step, and the curvature
        // dropped grows as the square. Refusing is the honest answer.
        if (stepSeconds > maxStep) return State.Skipped;

        dt = stepSeconds;
        return State.Run;
    }

    /// <summary>
    /// Player time while the world runs, and none while it is paused: the clock for how long
    /// something is <em>watched</em>, and the one place player time is the right answer.
    ///
    /// <para>Not the simulated step, because a view eased back or held on a moment answers the person
    /// looking, and through slow motion it would take ten times as long. Not raw player time either,
    /// because a pause is for looking: a view swung back behind the ball, or handed back, while
    /// nothing moves takes away what the player stopped the world to see.</para>
    /// </summary>
    /// <param name="playerSeconds">The wall-clock frame time.</param>
    /// <param name="stepSeconds">Simulated seconds the world just advanced by; zero while paused.</param>
    public static double Viewing(double playerSeconds, double stepSeconds)
        => double.IsFinite(stepSeconds) && stepSeconds > 0.0 && double.IsFinite(playerSeconds)
               ? Math.Max(0.0, playerSeconds)
               : 0.0;
}
