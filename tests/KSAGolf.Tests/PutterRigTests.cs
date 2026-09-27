using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// The putter at address: sole on the ground, face square to the target, shaft leaning up to the
/// kitten, and a swing that carries the head through the ball.
/// </summary>
public class PutterRigTests
{
    private static readonly PutterRig Rig = new();

    private static void Near(double3 expected, double3 actual, double within = 1e-6)
        => Assert.True(Vec.Len(expected - actual) < within, $"expected {expected}, got {actual}");

    [Fact]
    public void TheHeadSitsWhereTheRigSaysItDoes()
    {
        Near(Rig.HeadAtAddress, Rig.Place(PutterRig.AddressToFile(new double3(0, 0, PutterRig.HeadHeight / 2)), 0.0));
    }

    [Fact]
    public void TheSoleIsFlatOnTheGround()
    {
        foreach (double toe in new[] { -PutterRig.HeadHalfLength, 0.0, PutterRig.HeadHalfLength })
        {
            double3 sole = Rig.Place(PutterRig.AddressToFile(new double3(toe, 0, 0)), 0.0);
            Assert.True(Math.Abs(sole.Y - Rig.GroundY) < 1e-6, $"the sole at {toe} cm is at y = {sole.Y}");
        }
    }

    [Fact]
    public void TheFaceLooksAtTheTargetAndTheToeAwayFromTheKitten()
    {
        double3 centre = Rig.Place(PutterRig.AddressToFile(new double3(0, 0, 1.5)), 0.0);
        double3 face = Rig.Place(PutterRig.AddressToFile(new double3(0, 1, 1.5)), 0.0) - centre;
        double3 toe = Rig.Place(PutterRig.AddressToFile(new double3(1, 0, 1.5)), 0.0) - centre;

        Near(new double3(Rig.Scale, 0, 0), face);
        Near(new double3(0, 0, Rig.Scale), toe);
    }

    [Fact]
    public void TheGripIsAboveTheHeadAndTowardsTheKitten()
    {
        double3 grip = Rig.Grip(0.0);
        double3 head = Rig.HeadAtAddress;

        Assert.True(grip.Y > head.Y + 25.0, $"the grip is at {grip}");
        Assert.True(grip.Z < head.Z, "the shaft leans away from the kitten");
        Assert.True(Math.Abs(grip.X) < 1e-6, "the shaft leans off the target line");
    }

    [Fact]
    public void TheBallIsJustAheadOfTheFace()
    {
        double gap = (Rig.Ball.X - PutterRig.BallRadiusCm) - Rig.LeadingFaceX(0.0);

        Assert.True(Math.Abs(gap - Rig.AddressGap) < 1e-6, $"the gap is {gap} cm");
        Assert.True(Math.Abs(Rig.Ball.Y - PutterRig.BallRadiusCm - Rig.GroundY) < 1e-9, "the ball is not on the ground");
    }

    [Fact]
    public void AForwardSwingCarriesTheHeadTowardsTheTargetAndUp()
    {
        Assert.True(Rig.Head(0.3).X > Rig.Head(0.0).X);
        Assert.True(Rig.Head(-0.3).X < Rig.Head(0.0).X);
        Assert.True(Rig.SoleY(0.3) > Rig.SoleY(0.0));
        Assert.True(Math.Abs(Rig.SoleY(0.0) - Rig.GroundY) < 1e-6);
    }

    [Fact]
    public void TheWholeClubSwingsAsOne()
    {
        double3 grip = Rig.Grip(0.4) - Rig.Pivot;
        double3 head = Rig.Head(0.4) - Rig.Pivot;

        Assert.True(Math.Abs(Vec.Len(grip) - Vec.Len(Rig.Grip(0.0) - Rig.Pivot)) < 1e-6);
        Assert.True(Math.Abs(Vec.Len(head) - Vec.Len(Rig.HeadAtAddress - Rig.Pivot)) < 1e-6);
    }
}
