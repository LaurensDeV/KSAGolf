namespace KSAGolf;

/// <summary>
/// A distance as a reader wants it, which is not one unit across every range.
///
/// <para><b>A fixed unit goes blind as the distance it reports shrinks</b>: kilometres to one
/// decimal prints anything under 50 m as <c>0.0 km</c>, so a correction closing from kilometres
/// to metres reads as nothing at all.</para>
/// </summary>
internal static class Distance
{
    /// <summary>Below this a distance is said in metres, above it in kilometres.</summary>
    public const double KilometreFrom = 1_000.0;

    /// <summary>
    /// One distance, in the unit that shows it.
    ///
    /// <para>Absent rather than zero for a value that is not a number: a readout is being used to
    /// tell a converged loop from a broken one, and printing an unreadable state as <c>0.0 m</c>
    /// is the failure this whole class exists to stop.</para>
    /// </summary>
    public static string Say(double metres)
    {
        if (!double.IsFinite(metres)) return "unknown";

        return Math.Abs(metres) < KilometreFrom ? $"{metres:F1} m" : $"{metres / 1000.0:F2} km";
    }

    /// <summary>
    /// One distance for a line a script reads: metres to a tenth of a millimetre below a kilometre,
    /// and <see cref="Say"/>'s form above it, so a parser taking either unit reads both.
    ///
    /// <para>An endpoint resolves nothing finer than the line it is parsed from, so the harness's own
    /// lines carry more than a person's readout: a print step has to stay well under the spread a
    /// script is trying to resolve, or the quantum becomes part of the answer.</para>
    /// </summary>
    public static string Measure(double metres)
        => double.IsFinite(metres) && Math.Abs(metres) < KilometreFrom ? $"{metres:F4} m" : Say(metres);
}
