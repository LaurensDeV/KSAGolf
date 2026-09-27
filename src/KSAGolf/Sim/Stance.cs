using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// Where a kitten is drawn standing to address a ball: its model turned about +Y by <see cref="Yaw"/>
/// and moved by <see cref="Offset"/>, both in model space, so the rig's ball spot lands on the ball and
/// the rig's +X points at the target.
///
/// <para>Drawn rather than moved: the physics body stays where the player walked it, and only the
/// skeleton is carried the rest of the way. A landed kitten cannot be turned or moved by teleporting
/// it without the engine rebuilding its state from an orbit and throwing it about.</para>
/// </summary>
internal readonly record struct Stance(double Yaw, double3 Offset)
{
    /// <summary>
    /// The stance addressing a ball at <paramref name="ball"/> towards <paramref name="target"/>, both
    /// in model space, or false when the ball is further than <paramref name="reach"/> from where the
    /// kitten would have to stand.
    /// </summary>
    public static bool TryFor(double3 ball, double3 target, PutterRig rig, double reach, out Stance stance)
    {
        stance = default;
        double3 flat = new(target.X, 0.0, target.Z);
        if (Vec.Len2(flat) < 1e-12) return false;

        flat = Vec.Unit(flat);
        double yaw = Math.Atan2(-flat.Z, flat.X);
        var turned = new Stance(yaw, Vec.Zero);
        double3 offset = ball - turned.Place(rig.Ball, 1.0);

        if (Math.Sqrt((offset.X * offset.X) + (offset.Z * offset.Z)) > reach) return false;

        stance = new Stance(yaw, offset);
        return true;
    }

    /// <summary>The turn, part of the way in.</summary>
    public doubleQuat Rotation(double blend) => doubleQuat.CreateFromAxisAngle(new double3(0, 1, 0), Yaw * blend);

    /// <summary>A rig point in model space, part of the way in.</summary>
    public double3 Place(double3 rig, double blend) => double3.Transform(rig, Rotation(blend)) + (Offset * blend);
}
