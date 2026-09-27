using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

public class OrientTests
{
    [Fact]
    public void BothAxesArriveWhereTheyWereSent()
    {
        double3 face = Vec.Unit(new double3(0.2, 0.9, -0.1));
        double3 up = Vec.Unit(Vec.RejectFrom(new double3(0, 0, 1), face));

        doubleQuat q = Orient.CarryAxes(new double3(0, 0, 1), new double3(0, 1, 0), face, up);

        Assert.True(Vec.Len(double3.Transform(new double3(0, 0, 1), q) - face) < 1e-9);
        Assert.True(Vec.Len(double3.Transform(new double3(0, 1, 0), q) - up) < 1e-9);
    }

    [Fact]
    public void AnUpNotSquareToTheFacingIsMetAsNearlyAsItCanBe()
    {
        double3 face = new(1, 0, 0);
        doubleQuat q = Orient.CarryAxes(new double3(0, 0, 1), new double3(0, 1, 0), face, new double3(0.5, 0, 1));

        Assert.True(Vec.Len(double3.Transform(new double3(0, 1, 0), q) - new double3(0, 0, 1)) < 1e-9);
    }
}
