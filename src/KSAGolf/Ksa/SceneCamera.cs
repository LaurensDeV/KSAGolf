using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// A camera held for a composed shot, from the bridge's <c>frame</c>: set east, north and up of the craft
/// being flown, aimed at another craft or a body, at a field of view of its own. Everything is measured
/// against the followed craft the engine hands in and the separations sampled together, so nothing in it
/// carries a frame of the planet's travel.
/// </summary>
internal sealed class SceneCamera : IViewPose
{
    public required Vehicle Anchor { get; init; }
    public double3 EastNorthUp { get; init; }
    public Vehicle? AtCraft { get; init; }
    public double AtCraftUp { get; init; }
    public Celestial? AtBody { get; init; }
    public double FovDeg { get; init; } = 50.0;

    /// <summary>
    /// The anchor's own facing and head, in the ecliptic, when the offset is measured behind, right and
    /// above the anchor itself rather than east, north and up of the ground under it.
    /// </summary>
    public (double3 Face, double3 Head)? Body { get; init; }

    /// <summary>Degrees the view is turned right of its target, and tilted up, so the target sits off centre.</summary>
    public double TurnDeg { get; init; }
    public double TiltDeg { get; init; }

    public ChaseOrbit? Orbit => null;

    /// <summary>The eye's offset from the anchor and where it looks, in the ecliptic.</summary>
    public bool TryLook(out double3 offset, out double3 forward, out double3 up)
    {
        offset = forward = up = default;
        if (!KsaWorld.IsAlive(Anchor) || Anchor.Parent is not Celestial body) return false;

        double3 anchor = KsaWorld.PositionEcl(Anchor);
        up = Vec.Unit(anchor - KsaWorld.PositionEcl(body));
        double3 axis = new double3(0, 0, 1).Transform(body.GetCce2Ccf().Inverse());
        double3 north = Vec.Unit(axis - (up * Vec.Dot(axis, up)));
        double3 east = Vec.Cross(north, up);
        offset = (east * EastNorthUp.X) + (north * EastNorthUp.Y) + (up * EastNorthUp.Z);
        if (Body is { } own)
        {
            double3 side = Vec.Unit(Vec.Cross(own.Face, own.Head));
            offset = (own.Face * EastNorthUp.Y) + (side * EastNorthUp.X) + (own.Head * EastNorthUp.Z);
            up = own.Head;
            north = own.Head;
        }

        double3 target;
        if (AtCraft is { } craft && KsaWorld.IsAlive(craft)) target = KsaWorld.PositionEcl(craft) - anchor + (up * AtCraftUp);
        else if (AtBody is { } other) target = KsaWorld.PositionEcl(other) - anchor;
        else target = north * 100.0;

        forward = Vec.Unit(target - offset);

        // Looking straight down at the body, the vertical is along the view: north is the up then.
        if (Math.Abs(Vec.Dot(forward, up)) > 0.8) up = north;
        double3 right = Vec.Unit(Vec.Cross(forward, up));
        double turn = double.DegreesToRadians(TurnDeg), tilt = double.DegreesToRadians(TiltDeg);
        forward = Vec.Unit((forward * Math.Cos(turn)) + (right * Math.Sin(turn)));
        double3 top = Vec.Unit(Vec.Cross(right, forward));
        forward = Vec.Unit((forward * Math.Cos(tilt)) + (top * Math.Sin(tilt)));
        return Vec.IsFinite(forward) && Vec.IsFinite(offset);
    }

    /// <summary>Points the main view. Once a frame, from the camera pass.</summary>
    public bool Drive() => TryLook(out double3 offset, out double3 forward, out double3 up)
                           && KsaWorld.TryLookFromMainViewport(offset, forward, up, FovDeg, this);

    public bool TryPose(double3 followedEcl, out double3 offsetFromFollowed, out double3 forwardEcl, out double3 upEcl,
                        out double fovDeg)
    {
        fovDeg = FovDeg;
        return TryLook(out offsetFromFollowed, out forwardEcl, out upEcl);
    }
}
