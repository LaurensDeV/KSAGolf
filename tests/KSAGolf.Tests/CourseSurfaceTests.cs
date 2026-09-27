using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// The ball on a laid course: rails it banks off and cannot pass through at any speed, seams it does not
/// feel, a cup that holds a slow putt and lets a fast one run over, and a ramp a stopped ball rolls back down.
/// </summary>
public class CourseSurfaceTests
{
    private const double R = 6_371_000.0;
    private const double G = 9.81;
    private const double Mu = G * R * R;
    private const double Frame = 1.0 / 60.0;

    private static readonly double3 Up = new(0, 0, 1);

    private sealed class Sphere : IBodyGround
    {
        public bool TrySurfaceRadius(double3 dirCcf, out double radius)
        {
            radius = R;
            return true;
        }
    }

    private static Placed Put(string id, int i, int j, int turns = 0, bool mirrored = false, int level = 0)
        => new(PieceCatalogue.Find(id)!, new Cell(i, j), new Orientation(turns, mirrored), level);

    private static CourseSurface Lay(Course course, double headingDeg = 0.0)
    {
        double a = double.DegreesToRadians(headingDeg);
        var i = new double3(Math.Cos(a), Math.Sin(a), 0);
        var j = new double3(-Math.Sin(a), Math.Cos(a), 0);
        return new CourseSurface(course, Up * R, i, j, Up, new Sphere(), GolfBall.RadiusM);
    }

    private static double3 Ccf(CourseSurface s, double x, double y, double z) => (Up * R) + (Axis(s, 1, 0) * x) + (Axis(s, 0, 1) * y) + (Up * z);

    // The course's I and J in the body frame, read back through ToLocal.
    private static double3 Axis(CourseSurface s, double x, double y)
    {
        double3 ix = s.ToLocal((Up * R) + new double3(1, 0, 0)), iy = s.ToLocal((Up * R) + new double3(0, 1, 0));
        double3 i = Vec.Unit(new double3(ix.X, iy.X, 0)), j = Vec.Unit(new double3(ix.Y, iy.Y, 0));
        return (i * x) + (j * y);
    }

    private static GolfBall Rolling(CourseSurface s, double x, double y, double vx, double vy, double z = 0.0)
    {
        var ball = new GolfBall();
        ball.Place(Ccf(s, x, y, z + GolfBall.RadiusM));
        ball.Strike(Axis(s, vx, vy));
        return ball;
    }

    private static double3 Local(CourseSurface s, GolfBall ball) => s.ToLocal(ball.PositionCcf);

    [Fact]
    public void ABankShotLeavesTheRailAtTheAngleItArrived()
    {
        CourseSurface s = Lay(new Course([Put("straight3", 0, 0)]));
        GolfBall ball = Rolling(s, 0.5, 0.2, 2.0, 2.0);

        double3 before = default, after = default;
        for (int k = 0; k < 600 && after == default; k++)
        {
            double3 v = s.ToLocal((Up * R) + ball.VelocityCcf);
            ball.Step(1.0 / 600.0, s, Mu, 1.0, s);
            double3 w = s.ToLocal((Up * R) + ball.VelocityCcf);
            if (v.X > 0 && w.X < 0) (before, after) = (v, w);
        }

        Assert.True(after != default, "the ball never met the rail");
        Assert.True(Math.Abs((-after.X / before.X) - GolfBall.WallRestitution) < 0.05, $"came off at {-after.X / before.X:F2} of the speed into it");
        Assert.True(after.Y > 0.9 * before.Y, $"lost {1 - (after.Y / before.Y):P0} along the rail");
    }

    [Theory]
    [InlineData(4.0)]
    [InlineData(7.0)]
    [InlineData(10.0)]
    [InlineData(15.0)]
    public void NoSpeedCarriesABallThroughARail(double speed)
    {
        for (int phase = 0; phase < 40; phase++)
        {
            CourseSurface s = Lay(new Course([Put("straight3", 0, 0)]));
            GolfBall ball = Rolling(s, 0.5 + (phase / 40.0 * speed * Frame), 1.5, speed, 0.0);

            for (int k = 0; k < 60; k++)
            {
                ball.Step(Frame, s, Mu, 1.0, s);
                double x = Local(s, ball).X;
                Assert.True(x < 0.90 - GolfBall.RadiusM + 5e-4 && x > 0.10 + GolfBall.RadiusM - 5e-4, $"at {speed} m/s, phase {phase}, the ball reached x = {x:F4}");
            }
        }
    }

    [Fact]
    public void ABallRollsFromPieceToPieceWithoutFeelingTheSeams()
    {
        CourseSurface s = Lay(new Course([Put("straight", 0, 0), Put("straight", 0, 1, turns: 2), Put("straight3", 0, 2), Put("straight", 0, 5, turns: 2)]));
        GolfBall ball = Rolling(s, 0.5, 0.2, 0.0, 3.0);

        for (int k = 0; k < 90; k++)
        {
            ball.Step(Frame, s, Mu, 1.0, s);
            Assert.True(Math.Abs(Local(s, ball).Z - GolfBall.RadiusM) < 1e-4, $"the ball left the felt by {(Local(s, ball).Z - GolfBall.RadiusM) * 1000:F2} mm at y = {Local(s, ball).Y:F2}");
        }

        Assert.Equal(0, ball.Impacts);
        Assert.True(Local(s, ball).Y > 3.0, "the ball stopped short");
    }

    private static (bool InCup, double FurthestY) Putt(double speed)
    {
        CourseSurface s = Lay(new Course([Put("tee", 0, 0), Put("cup", 0, 1)]));
        GolfBall ball = Rolling(s, 0.5, 0.5, 0.0, speed);

        double furthest = 0.0;
        for (int k = 0; k < 900 && !ball.Resting; k++)
        {
            ball.Step(Frame, s, Mu, 1.0, s);
            furthest = Math.Max(furthest, Local(s, ball).Y);
        }

        return (s.InCup(ball.PositionCcf), furthest);
    }

    [Fact]
    public void ASlowPuttDropsIntoTheCup()
    {
        (bool inCup, double furthest) = Putt(1.35);

        Assert.True(inCup, "a putt dying at the hole stayed out");
        Assert.True(furthest < 1.5 + PieceShape.CupRadiusM, $"it ran on to y = {furthest:F2}");
    }

    [Fact]
    public void AFastPuttRunsOverTheCup()
    {
        (_, double furthest) = Putt(3.3);

        Assert.True(furthest > 1.5 + PieceShape.CupRadiusM, "a hard putt dropped in on its way over");
    }

    [Fact]
    public void ABallStoppedOnARampRollsBackDown()
    {
        CourseSurface s = Lay(new Course([Put("tee", 0, 0), Put("ramp", 0, 1)]));
        GolfBall ball = Rolling(s, 0.5, 2.0, 0.0, 0.0, z: 0.5 * CourseSize.LevelM);

        for (int k = 0; k < 180; k++) ball.Step(Frame, s, Mu, 1.0, s);

        Assert.True(Local(s, ball).Y < 1.2, $"the ball stayed on the ramp at y = {Local(s, ball).Y:F2}");
    }

    [Fact]
    public void AHoleTurnedOnTheGroundPlaysTheSame()
    {
        Course course = new([Put("tee", 0, 0), Put("corner", 0, 1), Put("room2", 1, 1, turns: 3), Put("cup", 3, 2, turns: 3)]);
        double3 Path(double heading)
        {
            CourseSurface s = Lay(course, heading);
            GolfBall ball = Rolling(s, 0.5, 0.3, 0.3, 2.5);
            for (int k = 0; k < 120; k++) ball.Step(Frame, s, Mu, 1.0, s);
            return Local(s, ball);
        }

        double3 a = Path(0.0), b = Path(37.0);
        Assert.True(Vec.Len(a - b) < 1e-6, $"turned 37 degrees the ball ended {Vec.Len(a - b) * 1000:F3} mm elsewhere");
    }

    [Fact]
    public void APieceMapsOntoTheCourseAndBack()
    {
        foreach (PieceDef def in PieceCatalogue.All)
        {
            foreach (Orientation o in Orientation.All)
            {
                var piece = new Placed(def, new Cell(-2, 5), o, 0);
                Cell tile = def.LocalCells().Last();
                var p = new double2((tile.I + 0.31) * CourseSize.CellM, (tile.J + 0.77) * CourseSize.CellM);
                double2 there = piece.ToCourse(p);

                Assert.True(double2.Distance(piece.ToPiece(there), p) < 1e-12, $"{def.Id} {o}");
                Assert.True(piece.Covers(BuildView.CellAt(there)), $"{def.Id} {o} put its last tile outside its cells");
            }
        }
    }
}
