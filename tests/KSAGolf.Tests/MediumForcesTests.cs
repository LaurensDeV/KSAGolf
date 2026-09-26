using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// The two forces a medium exerts, called directly -- including the guards: a body in a vacuum, at
/// rest, or declaring no drag has to come away with exactly nothing.
/// </summary>
public class MediumForcesTests
{
    private static readonly double3 Gravity = new(0, -9.81, 0);

    private const double DragK = 0.0002;

    // ---- Buoyancy ---------------------------------------------------------

    /// <summary>
    /// Zero switches the whole term off, which is what lets every body that only ever moves through air
    /// behave exactly as it would if buoyancy had never been added.
    /// </summary>
    [Fact]
    public void ABodyWithNoNeutralDensityJustFalls()
    {
        Assert.Equal(Gravity, Medium.Buoyancy(Gravity, 0.0, 1.0));
        Assert.Equal(Gravity, Medium.Buoyancy(Gravity, 0.0, 840.0));
    }

    /// <summary>A body at its neutral density neither sinks nor rises.</summary>
    [Fact]
    public void AtNeutralDensityGravityCancels()
    {
        double3 a = Medium.Buoyancy(Gravity, 840.0, 840.0);

        Assert.True(Vec.Len(a) < 1e-9, $"a neutral body accelerates at {Vec.Len(a):F6} m/s²");
    }

    /// <summary>Denser than the medium it still sinks; lighter and it rises.</summary>
    [Fact]
    public void DenserSinksAndLighterRises()
    {
        Assert.True(Medium.Buoyancy(Gravity, 840.0, 400.0).Y < 0.0);
        Assert.True(Medium.Buoyancy(Gravity, 840.0, 1200.0).Y > 0.0);
    }

    // ---- Drag -------------------------------------------------------------

    /// <summary>
    /// Nothing in a vacuum, nothing at rest, nothing for a body that declares no drag. Each of
    /// these divides out a zero somewhere if it is not guarded, and a NaN in an acceleration is a
    /// body that leaves the world on the next step.
    /// </summary>
    [Fact]
    public void DragIsExactlyNothingWhereItCannotApply()
    {
        double3 moving = new(300, 0, 0);

        Assert.Equal(Vec.Zero, Medium.Drag(moving, DragK, 0.0));
        Assert.Equal(Vec.Zero, Medium.Drag(Vec.Zero, DragK, 1.0));
        Assert.Equal(Vec.Zero, Medium.Drag(moving, 0.0, 1.0));
    }

    /// <summary>Opposes the airspeed, which is what makes it a deceleration to subtract.</summary>
    [Fact]
    public void DragActsAlongTheAirspeed()
    {
        double3 drag = Medium.Drag(new double3(300, 0, 0), DragK, 1.0);

        Assert.True(drag.X > 0.0, "the returned vector is subtracted, so it points along the motion");
        Assert.Equal(0.0, drag.Y, 12);
        Assert.Equal(0.0, drag.Z, 12);
    }

    /// <summary>
    /// Quadratic in airspeed: twice the speed is four times the drag. This is what makes a coasting
    /// body bleed speed instead of holding it.
    /// </summary>
    [Fact]
    public void DragGoesAsTheSquareOfAirspeed()
    {
        double slow = Vec.Len(Medium.Drag(new double3(100, 0, 0), DragK, 1.0));
        double fast = Vec.Len(Medium.Drag(new double3(200, 0, 0), DragK, 1.0));

        Assert.Equal(4.0, fast / slow, 9);
    }

    /// <summary>And linear in the medium's density, so one constant is right at every altitude.</summary>
    [Fact]
    public void DragScalesWithTheMediumsDensity()
    {
        double3 v = new(300, 0, 0);

        double seaLevel = Vec.Len(Medium.Drag(v, DragK, 1.0));
        double thin = Vec.Len(Medium.Drag(v, DragK, 0.25));

        Assert.Equal(0.25, thin / seaLevel, 9);
    }
}
