using Brutal.Numerics;

namespace KSAGolf;

/// <summary>What a hull test could establish about a moving body's step.</summary>
internal enum HullVerdict
{
    /// <summary>
    /// No geometry to ask. The caller keeps whatever the bounding sphere decided.
    ///
    /// <para>Never treated as a miss. A craft the test cannot resolve — one drawn by the character
    /// renderer rather than by parts, one whose mesh has not loaded, one the engine threw on —
    /// would otherwise become silently untouchable, which is worse than a contact registered wide
    /// and far harder to notice.</para>
    /// </summary>
    Unknown,

    /// <summary>The mover passes the body without touching it.</summary>
    Missed,

    /// <summary>The mover meets the surface, at a known fraction of its step.</summary>
    Struck,
}

/// <summary>
/// Whether a moving body actually meets a craft's surface, as opposed to entering the sphere that
/// contains it.
///
/// <para>A craft's bounding sphere is the half-diagonal of its bounding box — tens of metres for a
/// rocket, and built for orbital clearance margins rather than for its skin. A contact tested
/// against it registers at a distance nobody watching would call a hit.</para>
///
/// <para>The implementation lives in <c>Ksa/</c>, because only the engine knows where a hull is.
/// This seam is deliberately narrow: it is handed two <em>differences</em> and an opaque handle,
/// never an absolute position, so the ecliptic carrier cannot leak through it and there is no
/// frame-bearing subtraction at a call site no test reaches. See docs/FRAMES-AND-EPOCHS.md.</para>
/// </summary>
internal interface IHullTest
{
    /// <param name="body">Opaque handle to the body, never dereferenced here.</param>
    /// <param name="separation">Body centre minus mover position, both at the mover's own epoch.</param>
    /// <param name="travel">The mover's displacement across the step <em>relative to the body</em>,
    /// so the body stands still for the query and a static segment is the right question.</param>
    /// <param name="fraction">Where along <paramref name="travel"/> the surface was met, 0 to 1.</param>
    /// <param name="part">Opaque handle to the piece of the body that was met first, or null when the
    /// answer came from anything coarser than the body's own pieces.</param>
    HullVerdict Judge(object? body, double3 separation, double3 travel, out double fraction, out object? part);
}
