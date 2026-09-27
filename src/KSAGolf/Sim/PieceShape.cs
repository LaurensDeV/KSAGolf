using Brutal.Numerics;

namespace KSAGolf;

/// <summary>A rail or block seen from above, in a piece's own metres: its top stands <see cref="PieceShape.RailHeightM"/> above the floor.</summary>
internal readonly record struct RailBox(double X0, double Y0, double X1, double Y1)
{
    public bool Contains(double2 p) => p.X >= X0 && p.X <= X1 && p.Y >= Y0 && p.Y <= Y1;
}

/// <summary>A round bumper post, in a piece's own metres.</summary>
internal readonly record struct Post(double2 Centre, double Radius);

/// <summary>
/// What a piece is made of, exactly: its rails and posts, and its cup. The ball collides with these and the
/// Blender models are built from the same numbers, so what is drawn and what is hit cannot drift apart.
///
/// <para>Unturned, in the piece's own metres with its footprint's south-west corner at the origin and the
/// felt at zero. Every port is flanked the same way: a rail band from <see cref="RailOuterM"/> to
/// <see cref="RailInnerM"/> in from the edge on each side of the opening, running flush to the edge, so any
/// two pieces meet rail to rail and the lane between them is <see cref="CourseSize.LaneM"/> wide.</para>
/// </summary>
internal sealed record PieceShape(RailBox[] Rails, Post[] Posts, double2? Cup)
{
    public const double RailOuterM = 0.04;
    public const double RailInnerM = 0.10;

    /// <summary>Taller than the ball's radius, so the ball meets a rail's side and never its top edge while rolling.</summary>
    public const double RailHeightM = 0.05;

    /// <summary>The base under the felt, which also skirts uneven ground.</summary>
    public const double BaseDepthM = 0.10;

    public const double CupRadiusM = 0.054;
    public const double CupDepthM = 0.09;

    private const double A = RailOuterM, B = RailInnerM, C = 1.0 - RailInnerM, D = 1.0 - RailOuterM;

    private static RailBox R(double x0, double y0, double x1, double y1) => new(x0, y0, x1, y1);

    private static readonly Dictionary<string, PieceShape> Shapes = new()
    {
        ["tee"] = new([R(A, A, B, 1), R(C, A, D, 1), R(A, A, D, B)], [], null),
        ["cup"] = new([R(A, 0, B, D), R(C, 0, D, D), R(A, C, D, D)], [], new double2(0.5, 0.5)),
        ["straight"] = new([R(A, 0, B, 1), R(C, 0, D, 1)], [], null),
        ["straight3"] = new([R(A, 0, B, 3), R(C, 0, D, 3)], [], null),
        ["corner"] = new([R(A, 0, B, D), R(B, C, 1, D), R(C, 0, D, B), R(D, A, 1, B)], [], null),
        ["bend2"] = new([R(A, 0, B, 2 - A), R(B, 2 - B, 2, 2 - A), R(C, 0, D, 1 + B), R(D, 1 + A, 2, 1 + B)], [], null),
        ["sbend"] = new(
        [
            R(A, 0, B, 2 - A), R(B, 2 - B, 1 + B, 2 - A), R(1 + A, 2 - A, 1 + B, 3),
            R(C, 0, D, 1 + A), R(C, 1 + A, 2 - A, 1 + B), R(2 - B, 1 + B, 2 - A, 3),
        ], [], null),
        ["ramp"] = new([R(A, 0, B, 2), R(C, 0, D, 2)], [], null),
        ["room2"] = new(
        [
            R(A, 0, B, 2), R(2 - B, 0, 2 - A, 2),
            R(C, 0, D, B), R(D, A, 2 - B, B),
            R(B, 2 - B, 1 + A, 2 - A), R(1 + A, 2 - B, 1 + B, 2),
        ], [], null),
        ["room4"] = new(
        [
            R(A, 0, B, 4), R(4 - B, 0, 4 - A, 4),
            R(B, A, 1 + A, B), R(1 + A, 0, 1 + B, B), R(1 + C, 0, 1 + D, B), R(1 + D, A, 4 - B, B),
            R(B, 4 - B, 2 + A, 4 - A), R(2 + A, 4 - B, 2 + B, 4), R(2 + C, 4 - B, 2 + D, 4), R(2 + D, 4 - B, 4 - B, 4 - A),
        ],
        [new(new double2(1.55, 1.45), 0.09), new(new double2(2.45, 2.2), 0.09), new(new double2(1.6, 2.95), 0.09)],
        null),
    };

    public static PieceShape Of(PieceDef def) => Shapes.TryGetValue(def.Id, out PieceShape? shape) ? shape : new PieceShape([], [], null);

    /// <summary>
    /// The felt's height under a point of a piece, in its own metres, above the piece's level: flat, a straight
    /// rise between two ports at different levels, and in a cup a dip that a ball rides down into.
    /// </summary>
    /// <param name="ballRadius">
    /// The ball's, because a cup's rim is not a surface under the ball's centre: a ball over the hole rests on
    /// the rim's edge, lower the further in it is, and only once its centre is a radius inside does it drop.
    /// </param>
    public double FloorAt(double2 local, double ballRadius)
    {
        if (Cup is not { } cup) return 0.0;

        double d = double2.Distance(local, cup), inside = CupRadiusM - d;
        if (inside <= 0.0) return 0.0;

        // The centre of a ball resting on the rim's edge, less the radius, is the surface the ball sees.
        return inside < ballRadius ? Math.Sqrt((ballRadius * ballRadius) - (inside * inside)) - ballRadius : -CupDepthM;
    }

    /// <summary>Whether the ball has come to rest in the cup: its centre inside the hole and below the rim.</summary>
    public bool Holds(double2 local, double heightAboveFelt, double ballRadius)
        => Cup is { } cup && double2.Distance(local, cup) < CupRadiusM - ballRadius && heightAboveFelt < 0.0;
}
