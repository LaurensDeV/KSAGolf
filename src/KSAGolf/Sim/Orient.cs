using Brutal.Numerics;

namespace KSAGolf;

internal static class Orient
{
    /// <summary>
    /// The rotation carrying one facing-and-up pair onto another: the facing exactly, and the up as
    /// near as the facing allows.
    /// </summary>
    public static doubleQuat CarryAxes(double3 fromFace, double3 fromUp, double3 toFace, double3 toUp)
    {
        doubleQuat first = Vec.RotationFromTo(fromFace, toFace);
        double3 face = Vec.Unit(toFace);
        double3 upNow = Vec.Unit(Vec.RejectFrom(double3.Transform(fromUp, first), face));
        double3 want = Vec.Unit(Vec.RejectFrom(toUp, face));
        double roll = Math.Atan2(Vec.Dot(face, Vec.Cross(upNow, want)), Vec.Dot(upNow, want));

        return doubleQuat.Concatenate(first, doubleQuat.CreateFromAxisAngle(face, roll));
    }
}
