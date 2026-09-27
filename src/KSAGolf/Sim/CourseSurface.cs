using Brutal.Numerics;

namespace KSAGolf;

/// <summary>Something a moving ball can run into over one step, answered with where along the step and which way it faces.</summary>
internal interface IWalls
{
    /// <param name="fraction">How far along the step the ball first touches, 0 to 1.</param>
    /// <param name="normalCcf">The contact's normal, pointing back at the ball.</param>
    bool Sweep(double3 fromCcf, double3 toCcf, double radius, out double fraction, out double3 normalCcf);
}

/// <summary>
/// A laid course as the ball meets it: the felt as ground wherever a piece lies, rails and posts as walls,
/// and the ground beneath it everywhere else.
///
/// <para>Computed in the course's own flat frame -- over a course's few metres a planet's curve is a few
/// hundredths of a millimetre -- and handed to the ball in its body-fixed one. Everything is exact: rails
/// are swept rather than sampled, so no speed carries a ball through one, and pieces meet at exactly equal
/// heights, so a seam is not a bump.</para>
/// </summary>
internal sealed class CourseSurface : IBodyGround, IWalls
{
    // Rails are this far apart before a sweep bothers with them, in metres past the step's own reach.
    private const double Broad = 0.2;

    private readonly Course _course;
    private readonly IBodyGround _beyond;
    private readonly double3 _origin, _i, _j, _up;
    private readonly double _ballRadius;
    private readonly (Placed Piece, PieceShape Shape, RailBox[] Rails, (double2 Centre, double Radius)[] Posts)[] _pieces;

    /// <param name="originCcf">The course's grid origin on its level-0 felt, body-fixed.</param>
    /// <param name="beyond">The ground wherever no piece lies.</param>
    public CourseSurface(Course course, double3 originCcf, double3 iCcf, double3 jCcf, double3 upCcf, IBodyGround beyond, double ballRadius)
    {
        _course = course;
        _beyond = beyond;
        _origin = originCcf;
        _i = iCcf;
        _j = jCcf;
        _up = upCcf;
        _ballRadius = ballRadius;
        _pieces = [.. course.Pieces.Select(p =>
        {
            PieceShape shape = PieceShape.Of(p.Def);
            RailBox[] rails = [.. shape.Rails.Select(r => Laid(p, r))];
            (double2, double)[] posts = [.. shape.Posts.Select(q => (p.ToCourse(q.Centre), q.Radius))];
            return (p, shape, rails, posts);
        })];
    }

    public Course Course => _course;

    // A rail's box laid on the course: turns and mirrors keep boxes square to the grid.
    private static RailBox Laid(Placed piece, RailBox box)
    {
        double2 a = piece.ToCourse(new double2(box.X0, box.Y0)), b = piece.ToCourse(new double2(box.X1, box.Y1));
        return new RailBox(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
    }

    public double3 ToLocal(double3 ccf)
    {
        double3 d = ccf - _origin;
        return new double3(Vec.Dot(d, _i), Vec.Dot(d, _j), Vec.Dot(d, _up));
    }

    private double3 DirectionToCcf(double3 local) => (_i * local.X) + (_j * local.Y) + (_up * local.Z);

    private double3 DirectionToLocal(double3 ccf) => new(Vec.Dot(ccf, _i), Vec.Dot(ccf, _j), Vec.Dot(ccf, _up));

    /// <summary>The piece under a course-local point, or -1.</summary>
    public int PieceAt(double2 local)
    {
        var cell = new Cell((int)Math.Floor(local.X / CourseSize.CellM), (int)Math.Floor(local.Y / CourseSize.CellM));
        return _course.PieceAt(cell);
    }

    /// <summary>The felt's height at a course-local point, if a piece lies there.</summary>
    public bool TryFeltAt(double2 local, out double height)
    {
        height = 0.0;
        int at = PieceAt(local);
        if (at < 0) return false;

        (Placed piece, PieceShape shape, _, _) = _pieces[at];
        height = CourseLines.FloorAt(piece, local / CourseSize.CellM) + shape.FloorAt(piece.ToPiece(local), _ballRadius);
        return true;
    }

    public bool TrySurfaceRadius(double3 dirCcf, out double radius)
    {
        double along = Vec.Dot(dirCcf, _up);
        if (along > 0.5)
        {
            // Where the direction crosses the level-0 felt, then the felt's height there, found again once.
            double r = Vec.Dot(_origin, _up) / along;
            for (int k = 0; k < 2; k++)
            {
                double3 local = ToLocal(dirCcf * r);
                if (!TryFeltAt(new double2(local.X, local.Y), out double h)) break;

                r = (Vec.Dot(_origin, _up) + h) / along;
                if (k == 1)
                {
                    radius = r;
                    return true;
                }
            }
        }

        return _beyond.TrySurfaceRadius(dirCcf, out radius);
    }

    public bool TrySurfaceNormal(double3 dirCcf, out double3 normalCcf)
    {
        normalCcf = default;
        if (!TrySurfaceRadius(dirCcf, out double r)) return false;

        double3 local = ToLocal(dirCcf * r);
        var p = new double2(local.X, local.Y);
        const double Probe = 0.002;
        if (!TryFeltAt(p, out _) || !TryFeltAt(p + new double2(Probe, 0), out double east) || !TryFeltAt(p - new double2(Probe, 0), out double west)
            || !TryFeltAt(p + new double2(0, Probe), out double north) || !TryFeltAt(p - new double2(0, Probe), out double south))
        {
            return _beyond.TrySurfaceNormal(dirCcf, out normalCcf);
        }

        normalCcf = Vec.Unit(DirectionToCcf(new double3(-(east - west) / (2 * Probe), -(north - south) / (2 * Probe), 1.0)));
        return true;
    }

    /// <summary>Whether a ball here is resting in a cup.</summary>
    public bool InCup(double3 ballCcf)
    {
        double3 local = ToLocal(ballCcf);
        var p = new double2(local.X, local.Y);
        int at = PieceAt(p);
        if (at < 0) return false;

        (Placed piece, PieceShape shape, _, _) = _pieces[at];
        double felt = CourseLines.FloorAt(piece, p / CourseSize.CellM);
        return shape.Holds(piece.ToPiece(p), local.Z - _ballRadius - felt, _ballRadius);
    }

    public bool Sweep(double3 fromCcf, double3 toCcf, double radius, out double fraction, out double3 normalCcf)
    {
        fraction = 1.0;
        normalCcf = default;
        double3 a3 = ToLocal(fromCcf), b3 = ToLocal(toCcf);
        var a = new double2(a3.X, a3.Y);
        var d = new double2(b3.X - a3.X, b3.Y - a3.Y);
        double reach = double2.Distance(a, a + d) + radius + Broad;

        bool hit = false;
        double2 normal = default;
        foreach ((Placed piece, _, RailBox[] rails, (double2 Centre, double Radius)[] posts) in _pieces)
        {
            foreach (RailBox rail in rails)
            {
                if (!Near(rail, a, reach) || !Below(rail, piece, a3, radius)) continue;
                if (Swept.Box(a, d, rail, radius, out double t, out double2 n) && t < fraction)
                {
                    (fraction, normal, hit) = (t, n, true);
                }
            }

            foreach ((double2 centre, double postRadius) in posts)
            {
                if (double2.Distance(centre, a) > reach + postRadius) continue;
                if (Swept.Circle(a, d, centre, postRadius + radius, out double t, out double2 n) && t < fraction)
                {
                    (fraction, normal, hit) = (t, n, true);
                }
            }
        }

        if (hit) normalCcf = Vec.Unit(DirectionToCcf(new double3(normal.X, normal.Y, 0.0)));
        return hit;
    }

    private static bool Near(RailBox rail, double2 p, double reach)
        => p.X > rail.X0 - reach && p.X < rail.X1 + reach && p.Y > rail.Y0 - reach && p.Y < rail.Y1 + reach;

    // A rail stops a ball whose bottom is below its top; a ball in the air above it passes over.
    private static bool Below(RailBox rail, Placed piece, double3 ball, double radius)
    {
        double2 middle = new((rail.X0 + rail.X1) / 2.0, (rail.Y0 + rail.Y1) / 2.0);
        double top = CourseLines.FloorAt(piece, middle / CourseSize.CellM) + PieceShape.RailHeightM;
        return ball.Z - radius < top;
    }
}

/// <summary>Where a circle moving along a line first touches a shape, in the plane.</summary>
internal static class Swept
{
    /// <summary>
    /// A circle of <paramref name="radius"/> from <paramref name="a"/> along <paramref name="d"/> against a box:
    /// the box grown by the radius, its corners rounded. A circle already overlapping and moving further in is
    /// a hit at once, pushed out along the nearest face, so a ball that starts a step touching is not let through.
    /// </summary>
    public static bool Box(double2 a, double2 d, RailBox box, double radius, out double t, out double2 normal)
    {
        t = double.MaxValue;
        normal = default;

        // Already touching: whichever face it is least far through.
        double2 nearest = new(Math.Clamp(a.X, box.X0, box.X1), Math.Clamp(a.Y, box.Y0, box.Y1));
        double2 away = a - nearest;
        double gap = double2.Distance(a, nearest);
        if (gap < radius)
        {
            normal = gap > 1e-12 ? away / gap : Inside(a, box);
            t = 0.0;
            return double2.Dot(d, normal) < 0.0;
        }

        double best = double.MaxValue;
        double2 bestNormal = default;

        // The four faces, pushed out by the radius, each only where the path crosses it within the box's span.
        if (d.X > 0.0) Face((box.X0 - radius - a.X) / d.X, a.Y, d.Y, box.Y0, box.Y1, new double2(-1, 0), ref best, ref bestNormal);
        if (d.X < 0.0) Face((box.X1 + radius - a.X) / d.X, a.Y, d.Y, box.Y0, box.Y1, new double2(1, 0), ref best, ref bestNormal);
        if (d.Y > 0.0) Face((box.Y0 - radius - a.Y) / d.Y, a.X, d.X, box.X0, box.X1, new double2(0, -1), ref best, ref bestNormal);
        if (d.Y < 0.0) Face((box.Y1 + radius - a.Y) / d.Y, a.X, d.X, box.X0, box.X1, new double2(0, 1), ref best, ref bestNormal);

        // The rounded corners.
        foreach (double2 corner in (ReadOnlySpan<double2>)[new(box.X0, box.Y0), new(box.X1, box.Y0), new(box.X0, box.Y1), new(box.X1, box.Y1)])
        {
            if (Circle(a, d, corner, radius, out double tc, out double2 nc) && tc < best) (best, bestNormal) = (tc, nc);
        }

        if (best > 1.0) return false;

        (t, normal) = (best, bestNormal);
        return true;
    }

    private static void Face(double time, double start, double rate, double lo, double hi, double2 n, ref double best, ref double2 bestNormal)
    {
        if (time < 0.0 || time > 1.0 || time >= best) return;

        double along = start + (rate * time);
        if (along >= lo && along <= hi) (best, bestNormal) = (time, n);
    }

    private static double2 Inside(double2 p, RailBox box)
    {
        double[] gaps = [p.X - box.X0, box.X1 - p.X, p.Y - box.Y0, box.Y1 - p.Y];
        int k = Array.IndexOf(gaps, gaps.Min());
        return k switch { 0 => new(-1, 0), 1 => new(1, 0), 2 => new(0, -1), _ => new(0, 1) };
    }

    /// <summary>A point from <paramref name="a"/> along <paramref name="d"/> against a circle: when it first enters, and the outward normal there.</summary>
    public static bool Circle(double2 a, double2 d, double2 centre, double radius, out double t, out double2 normal)
    {
        t = 0.0;
        normal = default;
        double2 m = a - centre;
        double c = double2.Dot(m, m) - (radius * radius);
        if (c < 0.0)
        {
            // Inside already: a hit only if moving further in.
            double len = Math.Sqrt(double2.Dot(m, m));
            normal = len > 1e-12 ? m / len : new double2(1, 0);
            return double2.Dot(d, normal) < 0.0;
        }

        double aa = double2.Dot(d, d);
        if (aa < 1e-18) return false;

        double b = double2.Dot(m, d), disc = (b * b) - (aa * c);
        if (b >= 0.0 || disc < 0.0) return false;

        t = (-b - Math.Sqrt(disc)) / aa;
        if (t > 1.0) return false;

        normal = (m + (d * t)) / radius;
        return true;
    }
}
