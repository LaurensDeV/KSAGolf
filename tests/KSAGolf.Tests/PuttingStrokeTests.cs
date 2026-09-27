using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>The stroke: a backswing and a follow-through strike once, in proportion to how fast the hand moved.</summary>
public class PuttingStrokeTests
{
    private static readonly PutterRig Rig = new();
    private const double Frame = 1.0 / 60.0;

    // A backswing of so many pixels, then a steady stroke through at so many pixels a frame.
    private static (bool Struck, double Speed) Stroke(PuttingStroke stroke, double back, double perFrame, double towards = 1.0)
    {
        stroke.Update(-back * towards, Frame, towards, Rig, out _);
        for (int i = 0; i < 200; i++)
        {
            if (stroke.Update(perFrame * towards, Frame, towards, Rig, out double speed)) return (true, speed);
        }

        return (false, 0.0);
    }

    [Fact]
    public void ASwingThroughTheBallStrikesIt()
    {
        (bool struck, double speed) = Stroke(new PuttingStroke(), 80, 6);

        Assert.True(struck);
        Assert.True(speed > 0.3 && speed < 5.0, $"launched at {speed:F2} m/s");
    }

    [Fact]
    public void AFasterStrokeLaunchesTheBallFaster()
    {
        double slow = Stroke(new PuttingStroke(), 80, 4).Speed;
        double fast = Stroke(new PuttingStroke(), 80, 12).Speed;

        Assert.True(Math.Abs((fast / slow) - 3.0) < 0.05, $"three times the hand gave {fast / slow:F2} times the ball");
    }

    [Fact]
    public void TheBackswingDoesNotStrike()
    {
        var stroke = new PuttingStroke();
        for (int i = 0; i < 50; i++) Assert.False(stroke.Update(-6, Frame, 1.0, Rig, out _));
    }

    [Fact]
    public void TheBallIsStruckOnce()
    {
        var stroke = new PuttingStroke();
        Assert.True(Stroke(stroke, 80, 6).Struck);

        Assert.False(Stroke(stroke, 80, 6).Struck);
    }

    [Fact]
    public void WhichWayTheMouseGoesFollowsTheScreen()
    {
        Assert.True(Stroke(new PuttingStroke(), 80, 6, towards: -1.0).Struck);
    }

    [Fact]
    public void TheClubStopsAtTheEndOfItsSwing()
    {
        var stroke = new PuttingStroke();
        stroke.Update(-100_000, Frame, 1.0, Rig, out _);

        Assert.Equal(-PuttingStroke.MaxTheta, stroke.Theta);
    }

    /// <summary>Uneven mouse delivery at an even hand speed launches the ball as an even one would.</summary>
    [Fact]
    public void BurstyMouseInputIsAveragedOut()
    {
        var even = new PuttingStroke();
        var bursty = new PuttingStroke();
        even.Update(-80, Frame, 1.0, Rig, out _);
        bursty.Update(-80, Frame, 1.0, Rig, out _);

        double evenSpeed = 0.0, burstySpeed = 0.0;
        for (int i = 0; i < 200 && (evenSpeed == 0.0 || burstySpeed == 0.0); i++)
        {
            if (even.Update(6, Frame, 1.0, Rig, out double e)) evenSpeed = e;
            if (bursty.Update(i % 2 == 0 ? 12 : 0, Frame, 1.0, Rig, out double b)) burstySpeed = b;
        }

        Assert.True(Math.Abs((burstySpeed / evenSpeed) - 1.0) < 0.25, $"{burstySpeed:F2} against {evenSpeed:F2} m/s");
    }

    /// <summary>The club never passes through the ball: a stroke with no backswing still meets it.</summary>
    [Fact]
    public void AStrokeWithNoBackswingStillMeetsTheBall()
    {
        var stroke = new PuttingStroke();
        bool struck = false;
        for (int i = 0; i < 60 && !struck; i++) struck = stroke.Update(6, Frame, 1.0, Rig, out _);

        Assert.True(struck);
    }

    /// <summary>A hand that is not quite still at address does not touch the ball.</summary>
    [Fact]
    public void ATwitchAtAddressDoesNotReachTheBall()
    {
        var stroke = new PuttingStroke();
        Assert.False(stroke.Update(2, Frame, 1.0, Rig, out _));
    }
}
