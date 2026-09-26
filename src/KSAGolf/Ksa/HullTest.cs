using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Whether a moving body's step meets a craft's actual geometry, per triangle.
///
/// <para><c>Part.RayCastEgo</c> is watertight and is what KSA highlights parts with, the same
/// call the cursor picks craft through. The alternative, a craft's bounding sphere, is the
/// half-diagonal of its bounding box, a number built for orbital clearance margins: on a rocket it
/// stands ten metres clear of the skin, so a contact tested against it registers that far off the
/// hull.</para>
///
/// <para>No camera. <c>GetMatrixAsmb2Ego</c> takes the frame origin as an argument rather than
/// reading one off a viewport, so passing the mover-relative separation puts the whole cast in a
/// metres-scale frame centred on the mover — available in a paused world, a second viewport or no
/// viewport at all, and never evaluating geometry at ecliptic magnitudes.</para>
/// </summary>
internal sealed class HullTest : IHullTest
{
    /// <summary>Stateless, so everything in flight shares one.</summary>
    public static readonly HullTest Shared = new();

    // What a body the engine will not size is taken to be, in metres. Core's kitten is the only
    // craft with no part geometry at all, and its collider is a 0.4 m capsule 0.59 m from its
    // origin, so a metre covers it. KsaWorld.MeanRadius floors an unsized craft at 5 m, ten times
    // the cat: as a contact radius nothing can miss it.
    private const double UnsizedBodyRadius = 1.0;

    public HullVerdict Judge(object? body, double3 separation, double3 travel, out double fraction, out object? part)
    {
        fraction = 0.0;
        part = null;

        if (body is not Vehicle craft) return HullVerdict.Unknown;
        if (!Vec.IsFinite(separation) || !Vec.IsFinite(travel)) return HullVerdict.Unknown;

        double length = Vec.Len(travel);
        if (!(length > 0.0)) return HullVerdict.Unknown;

        try
        {
            if (craft.Parts is not { } tree) return HullVerdict.Unknown;

            ReadOnlySpan<Part> parts = tree.Parts;

            if (!KsaWorld.HasPickableMesh(parts))
            {
                return AgainstSphere(craft, separation, travel, out fraction);
            }

            double4x4 asmb2Round = craft.GetMatrixAsmb2Ego(separation);
            Ray ray = new() { Origin = default, Direction = travel / length };

            double nearest = double.MaxValue;
            int met = -1;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!parts[i].RayCastEgo(in asmb2Round, ray, out double near, out double far,
                                         out _, out _, out _, out _, out _, out _))
                {
                    continue;
                }

                // The cast is an unbounded ray and reports a negative near hit for an origin
                // already inside the hull, so both ends of the segment have to be imposed here.
                double hit = near >= 0.0 ? near : far >= 0.0 ? 0.0 : double.MaxValue;

                if (hit < nearest)
                {
                    nearest = hit;
                    met = i;
                }
            }

            if (nearest > length) return HullVerdict.Missed;

            fraction = nearest / length;
            part = parts[met];
            return HullVerdict.Struck;
        }
        catch
        {
            // A craft the engine will not answer for keeps the sphere. Answering Missed instead
            // would make it quietly untouchable, which is worse than a contact registered early and
            // much harder to notice.
            return HullVerdict.Unknown;
        }
    }

    // A craft with no mesh can still be missed. Nothing can be cast against a kitten: it is drawn
    // by the character renderer and its one part is declared empty, so its own sphere is the
    // answer, which is also what the engine picks one by.
    private static HullVerdict AgainstSphere(Vehicle craft, double3 separation, double3 travel,
                                             out double fraction)
    {
        double radius = craft.MeanRadius;
        if (!double.IsFinite(radius) || radius <= 0.0) radius = UnsizedBodyRadius;

        return ContactSweep.TryReachSphere(separation, travel, radius, out fraction)
                   ? HullVerdict.Struck
                   : HullVerdict.Missed;
    }
}
