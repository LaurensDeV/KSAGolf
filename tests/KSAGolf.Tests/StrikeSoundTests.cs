using Xunit;

namespace KSAGolf.Tests;

public class StrikeSoundTests
{
    [Theory]
    [InlineData(0.3, StrikeSound.Tap)]
    [InlineData(1.2, StrikeSound.Soft)]
    [InlineData(3.5, StrikeSound.Hard)]
    public void EachStrengthHasItsWhack(double speed, string expected) => Assert.Equal(expected, StrikeSound.ForStrike(speed).Id);

    [Fact]
    public void HarderIsNeverQuieterWithinAWhack()
    {
        foreach ((double low, double high) in new[] { (0.1, 0.5), (0.7, 2.0), (2.3, 6.0) })
        {
            Assert.True(StrikeSound.ForStrike(high).Volume >= StrikeSound.ForStrike(low).Volume);
        }
    }

    [Fact]
    public void VolumesStayInRange()
    {
        for (double v = 0.0; v < 20.0; v += 0.1)
        {
            Assert.InRange(StrikeSound.ForStrike(v).Volume, 0.0f, 1.0f);
            Assert.InRange(StrikeSound.ForLanding(v).Volume, 0.0f, 1.0f);
        }
    }
}
