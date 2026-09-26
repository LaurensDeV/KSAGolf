using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// The contact rule: whether something moving runs into a body over one step, rather than whether it
/// is near it at either end.
/// </summary>
public class ContactSweepTests
{
    /// <summary>
    /// A fast body crosses metres between samples, so a distance measured at both ends of a step
    /// says nothing about what happened in the middle.
    /// </summary>
    [Fact]
    public void AStepIsNotSteppedOver()
    {
        // Ends 500 m short and 500 m past, and dead centre in between.
        double3 separation = new(500, 0, 0);
        double3 closing = new(-1000, 0, 0);

        Assert.True(Vec.Len(separation) > 5.0);
        Assert.True(ContactSweep.TryContact(separation, closing, 1.0, 5.0,
                                            out double when, out double miss));
        Assert.Equal(0.5, when, 6);
        Assert.Equal(0.0, miss, 6);
    }

    /// <summary>Passing wide is not a contact, however briefly it was the nearest thing.</summary>
    [Fact]
    public void PassingWideIsAMiss()
    {
        Assert.False(ContactSweep.TryContact(new double3(500, 40, 0), new double3(-1000, 0, 0),
                                             1.0, 5.0, out _, out double miss));
        Assert.Equal(40.0, miss, 6);
    }

    /// <summary>
    /// The fallback for a body with nothing to cast against, answered in fractions of the step.
    /// Passing 3 m from something a metre across is a miss, and the size fed in is the only thing
    /// that decides it.
    /// </summary>
    [Fact]
    public void ReachingASphereIsDecidedByItsOwnSize()
    {
        double3 separation = new(10, 3, 0);
        double3 travel = new(20, 0, 0);

        Assert.False(ContactSweep.TryReachSphere(separation, travel, 1.0, out double fraction));
        Assert.True(ContactSweep.TryReachSphere(separation, travel, 5.0, out _));

        // Half way along the step, where the body draws abeam of it.
        Assert.Equal(0.5, fraction, 6);
    }

    /// <summary>
    /// A step that ends short of the body has not reached it. Without the clamp the closest
    /// approach is found beyond the step and a body strikes something it has not got to yet.
    /// </summary>
    [Fact]
    public void AStepThatEndsShortHasNotArrived()
    {
        Assert.False(ContactSweep.TryReachSphere(new double3(100, 0, 0), new double3(20, 0, 0),
                                                 5.0, out double fraction));
        Assert.Equal(1.0, fraction, 6);
    }
}
