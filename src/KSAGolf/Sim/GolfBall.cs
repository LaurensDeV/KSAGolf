using Brutal.Numerics;

namespace KSAGolf;

/// <summary>Where the ground is along a direction from a body's centre, in the body's own rotating frame.</summary>
internal interface IBodyGround
{
    /// <param name="dirCcf">Unit direction from the body's centre, body-fixed.</param>
    /// <param name="radius">Distance from the centre to the surface along it.</param>
    bool TrySurfaceRadius(double3 dirCcf, out double radius);

    /// <summary>The surface's normal along a direction, where it knows it exactly; otherwise the ball works it out from the radius.</summary>
    bool TrySurfaceNormal(double3 dirCcf, out double3 normalCcf)
    {
        normalCcf = default;
        return false;
    }
}

/// <summary>
/// A golf ball, simulated in the body-fixed frame of the world it lies on.
///
/// <para>Body-fixed so the ball carries none of the planet's orbit or spin: its position is epoch-free,
/// and drawing it only needs the body's own pose at the instant it is drawn. The rotating frame's
/// centrifugal and Coriolis terms are left out; at golf speeds on Earth they are 0.3% of gravity and
/// a hundredth of a millimetre a second, and the terrain's level is radial, so leaving them out keeps a
/// ball on flat ground still.</para>
/// </summary>
internal sealed class GolfBall
{
    public const double RadiusM = 0.021335;
    public const double MassKg = 0.04593;
    public const double DiameterMm = 42.67;
    public const double DragCoefficient = 0.25;

    public static readonly double DragK = Medium.DragK(MassKg, DiameterMm, DragCoefficient);

    /// <summary>Deceleration while rolling, as a fraction of gravity: a slowish green, about stimp 7.</summary>
    public const double RollingResistance = 0.08;

    /// <summary>The steepest slope, as rise over run, a stopped ball stays on.</summary>
    public const double RestSlope = 0.08;

    public const double Restitution = 0.35;

    /// <summary>Speed into the ground under which a ball stays on it rather than bouncing.</summary>
    public const double BounceBelow = 0.25;

    public const double RestSpeed = 0.01;

    public const double MaxSubStep = 1.0 / 240.0;

    /// <summary>
    /// A rise steeper than this, and taller than <see cref="StepUp"/>, is a wall the ball bounces off rather
    /// than ground it climbs: the side of a pad or a kerb, which a height taken straight down would
    /// otherwise lift it onto in one step.
    /// </summary>
    public const double WallSlope = 1.0;

    public const double StepUp = 0.01;

    // How far above the surface still counts as touching it: less than a frame's fall from rest.
    private const double ContactSlop = 0.002;

    // Half the baseline the surface's slope is measured across.
    private const double NormalProbe = 0.05;

    public double3 PositionCcf;
    public double3 VelocityCcf;
    public bool Resting;
    public bool OnGround;

    /// <summary>How many times the ball has hit the ground hard enough to be heard, and how hard the last was.</summary>
    public int Impacts { get; private set; }
    public double LastImpactSpeed { get; private set; }

    /// <summary>The slowest landing worth a sound.</summary>
    public const double AudibleImpact = 0.3;

    /// <summary>How the ball is turned, body-fixed, so a rolling ball is drawn rolling.</summary>
    public doubleQuat Spin = doubleQuat.Identity;

    public void Place(double3 positionCcf)
    {
        PositionCcf = positionCcf;
        VelocityCcf = Vec.Zero;
        Resting = false;
        OnGround = false;
    }

    public void Strike(double3 velocityCcf)
    {
        VelocityCcf = velocityCcf;
        Resting = false;
    }

    /// <param name="mu">The body's gravitational parameter.</param>
    /// <param name="densityRatio">The medium's density as a multiple of <see cref="Medium.ReferenceDensityKgPerM3"/>.</param>
    /// <param name="walls">Rails and posts the ball is swept against, besides the ground.</param>
    public void Step(double dt, IBodyGround ground, double mu, double densityRatio, IWalls? walls = null)
    {
        _walls = walls;
        if (Resting || !(dt > 0.0) || !Vec.IsFinite(PositionCcf)) return;

        int steps = Math.Clamp((int)Math.Ceiling(dt / MaxSubStep), 1, 2000);
        double h = dt / steps;

        for (int i = 0; i < steps && !Resting; i++) SubStep(h, ground, mu, densityRatio);
    }

    private void SubStep(double h, IBodyGround ground, double mu, double densityRatio)
    {
        double r = Vec.Len(PositionCcf);
        if (!(r > 0.0)) return;

        double3 up = PositionCcf / r;
        double3 gravity = up * (-mu / (r * r));

        bool known = ground.TrySurfaceRadius(up, out double surface);
        if (!known || r - (surface + RadiusM) > ContactSlop)
        {
            OnGround = false;
            VelocityCcf += (gravity - Medium.Drag(VelocityCcf, DragK, densityRatio)) * h;
            if (!known || !Walled(h, ground, up, surface)) Advance(h, up);
            return;
        }

        OnGround = true;
        double3 normal = SurfaceNormal(ground, up, surface);
        if (r < surface + RadiusM) PositionCcf = up * (surface + RadiusM);

        double into = Vec.Dot(VelocityCcf, normal);
        if (into < -AudibleImpact)
        {
            Impacts++;
            LastImpactSpeed = -into;
        }

        if (into < -BounceBelow)
        {
            VelocityCcf -= normal * ((1.0 + Restitution) * into);
            Advance(h, normal);
            return;
        }

        if (into < 0.0) VelocityCcf -= normal * into;

        double pressing = -Vec.Dot(gravity, normal);
        double3 downhill = gravity + (normal * pressing);
        double speed = Vec.Len(VelocityCcf);

        if (speed < RestSpeed && Vec.Len(downhill) <= RestSlope * pressing)
        {
            VelocityCcf = Vec.Zero;
            Resting = true;
            return;
        }

        double3 velocity = VelocityCcf + ((downhill - Medium.Drag(VelocityCcf, DragK, densityRatio)) * h);

        // Rolling resistance can stop the ball within a sub-step but never send it backwards.
        double rolling = speed > 1e-9 ? RollingResistance * pressing * h : 0.0;
        double moving = Vec.Len(velocity);
        velocity = moving > rolling ? velocity * ((moving - rolling) / moving) : Vec.Zero;

        VelocityCcf = velocity;
        if (!Walled(h, ground, up, surface)) Advance(h, normal);
    }

    // Whether this sub-step runs the ball into ground that rises like a wall, and if so turns it back.
    private bool Walled(double h, IBodyGround ground, double3 up, double surface)
    {
        double3 across = Vec.RejectFrom(VelocityCcf, up) * h;
        double travel = Vec.Len(across);
        if (travel < 1e-9) return false;

        double3 next = PositionCcf + (VelocityCcf * h);
        if (!ground.TrySurfaceRadius(Vec.Unit(next), out double ahead)) return false;

        double rise = ahead - surface;
        double clearance = Vec.Len(PositionCcf) - RadiusM - surface;
        if (rise - clearance <= StepUp || rise <= WallSlope * travel) return false;

        double3 along = Vec.Unit(across);
        VelocityCcf -= along * ((1.0 + Restitution) * Vec.Dot(VelocityCcf, along));
        return true;
    }

    /// <summary>A wall gives back this much of the speed into it: a painted wooden minigolf rail.</summary>
    public const double WallRestitution = 0.7;

    // Along a wall a ball keeps this much of its speed through a bounce.
    private const double WallGrip = 0.95;

    private IWalls? _walls;

    private void Advance(double h, double3 normal)
    {
        // Swept, so no speed carries the ball through a rail; a corner can take a second or third bounce.
        double left = 1.0;
        for (int bounce = 0; bounce < 4 && left > 1e-9; bounce++)
        {
            double3 to = PositionCcf + (VelocityCcf * (h * left));
            if (_walls is null || !_walls.Sweep(PositionCcf, to, RadiusM, out double fraction, out double3 wall))
            {
                PositionCcf = to;
                break;
            }

            PositionCcf += (to - PositionCcf) * fraction;
            left *= 1.0 - fraction;

            double into = Vec.Dot(VelocityCcf, wall);
            if (into < 0.0)
            {
                double3 along = VelocityCcf - (wall * into);
                VelocityCcf = (along * WallGrip) - (wall * (into * WallRestitution));
                if (-into > AudibleImpact)
                {
                    Impacts++;
                    LastImpactSpeed = -into;
                }
            }
        }

        // Rolling without slipping turns the ball about the axis across its path.
        double3 axis = Vec.Cross(normal, VelocityCcf);
        double rate = Vec.Len(axis) / RadiusM;
        if (OnGround && rate * h > 1e-9)
        {
            Spin = doubleQuat.Normalize(doubleQuat.Concatenate(Spin, doubleQuat.CreateFromAxisAngle(Vec.Unit(axis), rate * h)));
        }
    }

    /// <summary>The ground's normal under a point, from the height field's gradient across a short baseline.</summary>
    public static double3 SurfaceNormal(IBodyGround ground, double3 up, double surface)
    {
        if (ground.TrySurfaceNormal(up, out double3 exact) && Vec.IsFinite(exact)) return exact;

        double3 east = Vec.Unit(Vec.AnyPerpendicular(up));
        double3 north = Vec.Cross(up, east);
        double angle = NormalProbe / surface;

        if (!TrySlope(ground, up, surface, east, angle, out double alongEast)
            || !TrySlope(ground, up, surface, north, angle, out double alongNorth))
        {
            return up;
        }

        return Vec.Unit(up - (east * alongEast) - (north * alongNorth));
    }

    // The slope one way across a point. A side that rises or falls like a wall is left out, so the edge of
    // a kerb is not read as a ramp that throws the ball over it.
    private static bool TrySlope(IBodyGround ground, double3 up, double surface, double3 across, double angle, out double slope)
    {
        slope = 0.0;
        double3 ahead = Vec.Unit((up * Math.Cos(angle)) + (across * Math.Sin(angle)));
        double3 behind = Vec.Unit((up * Math.Cos(angle)) - (across * Math.Sin(angle)));
        if (!ground.TrySurfaceRadius(ahead, out double high) || !ground.TrySurfaceRadius(behind, out double low)) return false;

        double forward = (high - surface) / NormalProbe;
        double backward = (surface - low) / NormalProbe;
        bool wallAhead = Math.Abs(forward) > WallSlope, wallBehind = Math.Abs(backward) > WallSlope;

        slope = wallAhead && wallBehind ? 0.0 : wallAhead ? backward : wallBehind ? forward : (forward + backward) / 2.0;
        return double.IsFinite(slope);
    }
}
