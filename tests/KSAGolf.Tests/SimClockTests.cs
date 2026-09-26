using Xunit;

namespace KSAGolf.Tests;

/// <summary>
/// What the mod does with the simulation step KSA reports.
///
/// <para>Stepping on StarMap's player-time delta is wrong twice over: it is wall-clock, so it
/// keeps running through a pause and acts in a frozen world, and it stays at 1× under warp, so
/// the world outruns whatever the mod integrates.</para>
///
/// <para>And it has to be the step KSA applied rather than a clock differenced around it.
/// Differencing lands a step out of phase, which shows up as bodies zigzagging laterally with the
/// vertical axis clean — see <see cref="SimClock"/>.</para>
/// </summary>
public class SimClockTests
{
    [Fact]
    public void APausedGameAdvancesNothing()
    {
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(0.0, paused: true, out double dt));
        Assert.Equal(0.0, dt);
    }

    [Fact]
    public void APausedGameAdvancesNothingEvenIfAStepIsReported()
    {
        // Belt and braces. If a build ever reports a step while paused, the mod must still not
        // act in a frozen world.
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(0.05, paused: true, out double dt));
        Assert.Equal(0.0, dt);
    }

    [Fact]
    public void AnOrdinaryStepRuns()
    {
        Assert.Equal(SimClock.State.Run, SimClock.Classify(0.016, paused: false, out double dt));
        Assert.Equal(0.016, dt, 9);
    }

    [Fact]
    public void TheStepIsPassedThroughExactly()
    {
        // The whole point of reading the applied step is that it is not adjusted, rounded or
        // re-derived on the way through. Whatever KSA moved the world by is what a moving body
        // integrates over, or the difference reappears multiplied by 29.8 km/s.
        foreach (double step in new[] { 0.0011, 0.016, 0.0167, 0.019993, 0.2 })
        {
            Assert.Equal(SimClock.State.Run, SimClock.Classify(step, paused: false, out double dt));
            Assert.Equal(step, dt, 12);
        }
    }

    [Fact]
    public void ModestTimewarpStillRuns()
    {
        // 10x warp at 60 fps is ~0.167 s per step, well inside the default budget. This must
        // keep working, not stand down.
        Assert.Equal(SimClock.State.Run, SimClock.Classify(0.167, paused: false, out double dt));
        Assert.Equal(0.167, dt, 9);
    }

    [Fact]
    public void AStepTooLargeToIntegrateStandsDownRatherThanCoarsening()
    {
        Assert.Equal(SimClock.State.Skipped,
            SimClock.Classify(SimClock.MaxStep + 0.001, paused: false, out double dt));
        Assert.Equal(0.0, dt);
    }

    [Fact]
    public void AStepOfExactlyTheBudgetRuns()
    {
        Assert.Equal(SimClock.State.Run, SimClock.Classify(SimClock.MaxStep, paused: false, out double dt));
        Assert.Equal(SimClock.MaxStep, dt, 9);
    }

    [Fact]
    public void AZeroOrNegativeStepDoesNothing()
    {
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(0.0, paused: false, out _));
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(-0.016, paused: false, out double dt));
        Assert.Equal(0.0, dt);
    }

    [Fact]
    public void ANonFiniteStepDoesNothing()
    {
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(double.NaN, paused: false, out _));
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(double.PositiveInfinity, paused: false, out _));
    }

    [Fact]
    public void ThereIsNoStateToGetWrongAcrossASceneChange()
    {
        // Stateless by design: differencing would need priming, a reset on leaving flight, and a
        // rule for the clock going backwards on load - three ways to be wrong.
        Assert.Equal(SimClock.State.Run, SimClock.Classify(0.016, paused: false, out _));
        Assert.Equal(SimClock.State.Idle, SimClock.Classify(0.0, paused: true, out _));
        Assert.Equal(SimClock.State.Run, SimClock.Classify(0.016, paused: false, out double dt));
        Assert.Equal(0.016, dt, 9);
    }

    // ---- The viewing clock ------------------------------------------------

    /// <summary>
    /// What is watched stops with the world. Player time runs on through a pause, which would hand
    /// a held view back and swing a turned chase back behind its ball while nothing moved.
    /// </summary>
    [Fact]
    public void AViewIsHeldThroughAPause()
    {
        Assert.Equal(0.0, SimClock.Viewing(playerSeconds: 0.016, stepSeconds: 0.0));
    }

    /// <summary>
    /// And runs at the player's rate through slow motion, where the simulated step would make a
    /// three-second hold last five minutes at a hundredth of normal speed.
    /// </summary>
    [Theory]
    [InlineData(0.016)]
    [InlineData(0.00016)]
    [InlineData(0.16)]
    public void AViewKeepsThePlayersTimeWhileTheWorldRuns(double step)
    {
        Assert.Equal(0.016, SimClock.Viewing(playerSeconds: 0.016, stepSeconds: step));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(-0.016)]
    public void AStepThatSaysNothingHoldsTheView(double step)
    {
        Assert.Equal(0.0, SimClock.Viewing(playerSeconds: 0.016, stepSeconds: step));
    }
}
