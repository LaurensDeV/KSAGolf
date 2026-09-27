using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// A golf ball on a planet-sized sphere and on planes tilted across it: it lands, bounces, rolls
/// as far as its rolling resistance allows, runs down a slope and stays on a gentle one.
/// </summary>
public class GolfBallTests
{
    private const double R = 6_371_000.0;
    private const double G = 9.81;
    private const double Mu = G * R * R;

    /// <summary>A sphere, optionally tilted: the surface rises by <c>slope</c> per metre along <c>uphill</c>.</summary>
    private sealed class Ground(double3 uphill, double slope = 0.0) : IBodyGround
    {
        public bool TrySurfaceRadius(double3 dirCcf, out double radius)
        {
            radius = R + (slope * R * Vec.Dot(dirCcf, uphill));
            return true;
        }
    }

    private static readonly double3 Up = new(0, 0, 1);
    private static readonly double3 East = new(1, 0, 0);

    private static GolfBall OnSurface(double3 up)
    {
        var ball = new GolfBall();
        ball.Place(up * (R + GolfBall.RadiusM));
        return ball;
    }

    private static void Run(GolfBall ball, IBodyGround ground, double seconds)
    {
        for (double t = 0; t < seconds && !ball.Resting; t += 1.0 / 60.0) ball.Step(1.0 / 60.0, ground, Mu, 1.0);
    }

    private static double Clearance(GolfBall ball) => Vec.Len(ball.PositionCcf) - R - GolfBall.RadiusM;

    [Fact]
    public void ADroppedBallSettlesOnTheGround()
    {
        var ball = new GolfBall();
        ball.Place(Up * (R + 0.5));

        Run(ball, new Ground(East), 10.0);

        Assert.True(ball.Resting, "a dropped ball never came to rest");
        Assert.True(Math.Abs(Clearance(ball)) < 0.002, $"it rests {Clearance(ball) * 1000:F1} mm off the ground");
    }

    [Fact]
    public void ADroppedBallBouncesLowerThanItFell()
    {
        var ball = new GolfBall();
        ball.Place(Up * (R + GolfBall.RadiusM + 1.0));
        var ground = new Ground(East);

        bool landed = false;
        double highest = 0.0;
        for (int i = 0; i < 180; i++)
        {
            ball.Step(1.0 / 60.0, ground, Mu, 1.0);
            if (Vec.Dot(ball.VelocityCcf, Up) > 0.0) landed = true;
            if (landed) highest = Math.Max(highest, Clearance(ball));
        }

        // e² of the drop, give or take the step it lands in.
        Assert.InRange(highest, 0.5 * GolfBall.Restitution * GolfBall.Restitution, 2.0 * GolfBall.Restitution * GolfBall.Restitution);
    }

    [Fact]
    public void APuttRollsAsFarAsItsRollingResistanceAllows()
    {
        var ball = OnSurface(Up);
        ball.Strike(East * 2.0);

        Run(ball, new Ground(East), 20.0);

        double rolled = Vec.Len(ball.PositionCcf - (Up * (R + GolfBall.RadiusM)));
        double expected = 2.0 * 2.0 / (2.0 * GolfBall.RollingResistance * G);
        Assert.True(ball.Resting, "the putt never stopped");
        Assert.InRange(rolled, expected * 0.9, expected * 1.05);
    }

    /// <summary>The same putt anywhere on the planet goes the same distance: nothing depends on the frame's axes.</summary>
    [Fact]
    public void APuttGoesTheSameDistanceWhereverItIsStruck()
    {
        double Putt(double3 up, double3 along)
        {
            var ball = OnSurface(up);
            ball.Strike(along * 1.5);
            Run(ball, new Ground(along), 20.0);
            return Vec.Len(ball.PositionCcf - (up * (R + GolfBall.RadiusM)));
        }

        double3 tilted = Vec.Unit(new double3(0.3, -0.5, 0.8));
        double here = Putt(Up, East);
        double there = Putt(tilted, Vec.Unit(Vec.Cross(tilted, new double3(0, 0, 1))));

        Assert.True(Math.Abs(here - there) < 0.01, $"{here:F3} m against {there:F3} m");
    }

    [Fact]
    public void ABallOnASteepSlopeRunsDownhill()
    {
        var ball = OnSurface(Up);
        Run(ball, new Ground(East, slope: 0.15), 2.0);

        double downhill = -Vec.Dot(ball.PositionCcf, East);
        Assert.True(downhill > 0.5, $"it moved {downhill:F3} m downhill in two seconds");
    }

    [Fact]
    public void ABallOnAGentleSlopeStaysPut()
    {
        var ball = OnSurface(Up);
        Run(ball, new Ground(East, slope: 0.03), 2.0);

        Assert.True(ball.Resting, "a ball on a 3% slope never settled");
        Assert.True(Math.Abs(Vec.Dot(ball.PositionCcf, East)) < 0.01, "it crept off");
    }

    [Fact]
    public void ARestingBallStaysResting()
    {
        var ball = OnSurface(Up);
        Run(ball, new Ground(East), 1.0);
        double3 at = ball.PositionCcf;

        ball.Step(1.0, new Ground(East), Mu, 1.0);

        Assert.Equal(at, ball.PositionCcf);
    }

    [Fact]
    public void ARollingBallTurns()
    {
        var ball = OnSurface(Up);
        ball.Strike(East * 1.0);
        ball.Step(0.1, new Ground(East), Mu, 1.0);

        Assert.NotEqual(doubleQuat.Identity, ball.Spin);
    }

    [Fact]
    public void TheNormalOfATiltedPlaneLeansAwayFromUphill()
    {
        double3 normal = GolfBall.SurfaceNormal(new Ground(East, slope: 0.1), Up, R);

        Assert.True(Vec.Dot(normal, East) < -0.09, $"the normal is {normal}");
        Assert.True(Math.Abs(Vec.Len(normal) - 1.0) < 1e-9);
    }

    [Fact]
    public void ALandingIsCountedAndARollIsNot()
    {
        var dropped = new GolfBall();
        dropped.Place(Up * (R + GolfBall.RadiusM + 0.5));
        Run(dropped, new Ground(East), 5.0);

        var putt = OnSurface(Up);
        putt.Strike(East * 2.0);
        Run(putt, new Ground(East), 10.0);

        Assert.True(dropped.Impacts >= 1, "a dropped ball landed silently");
        Assert.InRange(dropped.LastImpactSpeed, GolfBall.AudibleImpact, 3.2);
        Assert.Equal(0, putt.Impacts);
    }

    /// <summary>Flat ground with a kerb: <c>height</c> higher beyond <c>at</c> metres east.</summary>
    private sealed class Kerb(double at, double height) : IBodyGround
    {
        public bool TrySurfaceRadius(double3 dirCcf, out double radius)
        {
            radius = R + (Vec.Dot(dirCcf, East) * R > at ? height : 0.0);
            return true;
        }
    }

    /// <summary>A height taken straight down would lift it onto the kerb in one step; a wall turns it back.</summary>
    [Theory]
    [InlineData(1.5)]
    [InlineData(4.0)]
    public void ABallRollingIntoAKerbBouncesOffIt(double speed)
    {
        var ball = OnSurface(Up);
        ball.Strike(East * speed);
        var kerb = new Kerb(0.5, 0.10);

        double highest = 0.0;
        for (int i = 0; i < 600 && !ball.Resting; i++)
        {
            ball.Step(1.0 / 60.0, kerb, Mu, 1.0);
            highest = Math.Max(highest, Clearance(ball));
        }

        Assert.True(highest < 0.05, $"it was lifted {highest * 100:F1} cm onto the kerb");
        Assert.True(Vec.Dot(ball.PositionCcf, East) < 0.5, "it came to rest past the kerb");
    }

    [Fact]
    public void ABallRollsUpAStepShorterThanItsReach()
    {
        var ball = OnSurface(Up);
        ball.Strike(East * 1.5);

        Run(ball, new Kerb(0.5, 0.005), 10.0);

        Assert.True(Vec.Dot(ball.PositionCcf, East) > 0.5, "a 5 mm lip stopped it");
    }
}
