using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// Where the ground is under a body in flight.
///
/// <para>Answered as a <b>centre and a surface radius</b> rather than as an altitude, and that is
/// the point of the shape: a body samples it once a frame and then knows its own height above the
/// ground at every sub-step for the cost of a subtraction. An altitude would have to be re-read
/// per sub-step to mean anything, and a terrain sample is the expensive call here.</para>
///
/// <para>The approximation it buys is that the surface under the body is treated as a sphere of
/// that radius for the frame. For something covering a few metres of ground track in one frame
/// that is exact except across a cliff edge, and a cliff is where the engine's own height query is
/// discontinuous anyway. Something covering tens of metres a frame on a slope is reading ground it
/// has already left, and has to ask again near the surface.</para>
///
/// <para>Unlike <see cref="IHullTest"/> this takes an absolute position — and that does <b>not</b>
/// make it epoch-free. An implementation has to turn the position into a direction against a body,
/// and the body it names is sampled at the frame's end while the mover is part-way through the
/// frame. So there are two epochs to mismatch after all, and both of the body's motions leak: its
/// travel, and its <em>spin</em>, which turns the ground under the query.</para>
///
/// <para>The correction stays on the asking side: this interface has no time argument, and the
/// camera asks it from places that have no frame phase.</para>
/// </summary>
internal interface IGroundTest
{
    /// <param name="positionEcl">Where the body is now.</param>
    /// <param name="centreEcl">Centre of the body beneath it.</param>
    /// <param name="surfaceRadius">Distance from that centre to the ground under the body.</param>
    bool TryGround(double3 positionEcl, out double3 centreEcl, out double surfaceRadius);
}

/// <summary>The surface a falling body actually meets, which over sea is the sea.</summary>
internal static class GroundSurface
{
    /// <summary>
    /// The higher of the terrain and the waterline, both as heights above the body's mean radius.
    ///
    /// <para>A height field answers with terrain and nothing else, so under an ocean it reports the
    /// <em>seabed</em>. A ball then falls straight through the waterline and lands on the bottom,
    /// where nobody sees it.</para>
    ///
    /// <para>Over land the terrain is above the waterline and wins, so nothing about dry ground
    /// changes. Bodies with no sea pass <paramref name="hasSea"/> false and are untouched.</para>
    /// </summary>
    public static double Height(double terrainHeight, double seaLevel, bool hasSea)
    {
        if (!double.IsFinite(terrainHeight)) return terrainHeight;
        if (!hasSea || !double.IsFinite(seaLevel)) return terrainHeight;

        return seaLevel > terrainHeight ? seaLevel : terrainHeight;
    }
}
