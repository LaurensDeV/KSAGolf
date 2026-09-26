using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// What the air or water a body moves through does to it: buoyancy and quadratic drag. Takes numbers
/// rather than a profile, so a ball, a cart and anything later ask for the same physics.
///
/// <para>Every velocity is measured in the <b>local</b> frame, against the ground the body is moving
/// over, never the ecliptic. See <c>docs/FRAMES-AND-EPOCHS.md</c>.</para>
/// </summary>
internal static class Medium
{
    /// <summary>
    /// Gravity as a body in this medium feels it. A <paramref name="neutralDensityRatio"/> of zero
    /// switches the term off, so a body that only ever moves through air is unaffected by it.
    /// </summary>
    public static double3 Buoyancy(double3 gravity, double neutralDensityRatio, double densityRatio)
        => neutralDensityRatio > 0.0 ? gravity * (1.0 - (densityRatio / neutralDensityRatio)) : gravity;

    /// <summary>
    /// The longest step worth integrating across while there is air to resolve: density falls off on a
    /// scale height of a few kilometres, and a fast body crosses that in seconds.
    /// </summary>
    public const double FaithfulStepInAir = 0.05;

    /// <summary>Below this the air cannot move the answer within one step, whatever the step.</summary>
    public const double NoticeableDensity = 1e-4;

    /// <summary>
    /// What a density ratio is a multiple of: Earth's sea-level air, 1.225 kg/m³, as KSA declares it. One
    /// reference for every body, so a body is dragged by the air actually there -- a fiftieth of it over a
    /// Mars-like surface and fifty times it over a Venus-like one.
    /// </summary>
    public const double ReferenceDensityKgPerM3 = 1.225;

    /// <summary>
    /// A sphere's drag constant from what it is: half the reference air's density, times its drag
    /// coefficient, times its frontal area, over its mass. Zero for anything not given.
    /// </summary>
    public static double DragK(double massKg, double diameterMm, double dragCoefficient)
    {
        if (!(massKg > 0.0) || !(diameterMm > 0.0) || !(dragCoefficient > 0.0)) return 0.0;

        double radius = diameterMm / 2000.0;
        return 0.5 * ReferenceDensityKgPerM3 * dragCoefficient * Math.PI * radius * radius / massKg;
    }

    /// <summary>
    /// Everything accelerating an unpowered body: the pull less its buoyancy, and its drag through the
    /// air at <paramref name="airVelocity"/>.
    /// </summary>
    public static double3 Coasting(double3 pull, double3 airVelocity, double dragK, double neutralDensityRatio,
                                   double densityRatio)
        => Buoyancy(pull, neutralDensityRatio, densityRatio) - Drag(airVelocity, dragK, densityRatio);

    /// <summary>
    /// The drag deceleration, as a vector to <b>subtract</b> from a body's acceleration: quadratic in
    /// airspeed and scaled by the medium's density, so one constant is right in air and in water.
    /// <paramref name="dragK"/> is the value in reference air, which is why the ratio is 1.0 there.
    ///
    /// <para>Zero in a vacuum, at rest, or with no drag declared -- guarded here rather than at the call
    /// sites so a body cannot be given a NaN direction by dividing out a zero speed.</para>
    /// </summary>
    public static double3 Drag(double3 localVelocity, double dragK, double densityRatio)
    {
        double airspeed = Vec.Len(localVelocity);
        if (dragK <= 0.0 || airspeed <= 1e-6 || densityRatio <= 0.0) return Vec.Zero;

        return localVelocity * (dragK * airspeed * densityRatio);
    }
}
