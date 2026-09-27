using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

public class StanceTests
{
    private static readonly PutterRig Rig = new();

    [Fact]
    public void TheRigsBallSpotLandsOnTheBall()
    {
        double3 ball = new(30.0, -1.0, 40.0);
        Assert.True(Stance.TryFor(ball, new double3(0.6, 0.1, -0.8), Rig, 100.0, out Stance stance));

        Assert.True(Vec.Len(stance.Place(Rig.Ball, 1.0) - ball) < 1e-9);
    }

    [Fact]
    public void TheRigsTargetPointsWhereTheTargetIsLevelled()
    {
        double3 target = new(0.6, 0.3, -0.8);
        Assert.True(Stance.TryFor(new double3(0, 0, 30), target, Rig, 100.0, out Stance stance));

        double3 aimed = double3.Transform(new double3(1, 0, 0), stance.Rotation(1.0));
        Assert.True(Vec.Len(aimed - Vec.Unit(new double3(0.6, 0.0, -0.8))) < 1e-9, $"aimed at {aimed}");
    }

    [Fact]
    public void AStanceAlreadyInPlaceNeedsNoMove()
    {
        Assert.True(Stance.TryFor(Rig.Ball, new double3(1, 0, 0), Rig, 1.0, out Stance stance));

        Assert.True(Vec.Len(stance.Offset) < 1e-9);
        Assert.True(Math.Abs(stance.Yaw) < 1e-12);
    }

    [Fact]
    public void ABallOutOfReachIsRefused()
    {
        Assert.False(Stance.TryFor(new double3(0, 0, 200), new double3(1, 0, 0), Rig, 60.0, out _));
    }

    [Fact]
    public void HalfWayInIsHalfTheMove()
    {
        Assert.True(Stance.TryFor(new double3(0, 0, 80), new double3(1, 0, 0), Rig, 100.0, out Stance stance));

        Assert.True(Vec.Len(stance.Place(Vec.Zero, 0.5) - (stance.Offset * 0.5)) < 1e-9);
        Assert.Equal(Vec.Zero, stance.Place(Vec.Zero, 0.0));
    }
}
