using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>The boxes a kitten stands on: their tops are the felt the ball rolls on, flat and up a ramp.</summary>
public class CourseCollidersTests
{
    // Whether a point of the course lies over a box, seen from above.
    private static bool Over(ColliderBox box, double2 p)
    {
        double3 d = new double3(p.X, p.Y, box.TopAt(p)) - box.Centre;
        return Math.Abs(Vec.Dot(d, box.X)) <= (box.Size.X / 2.0) + 1e-9 && Math.Abs(Vec.Dot(d, box.Y)) <= (box.Size.Y / 2.0) + 1e-9;
    }

    [Fact]
    public void EachSlabsTopIsTheFelt()
    {
        Course course = SampleCourse.Build();
        List<ColliderBox> boxes = CourseColliders.Of(course);

        foreach (Placed piece in course.Pieces)
        {
            foreach (Cell tile in piece.Cells())
            {
                for (double u = 0.1; u < 1.0; u += 0.2)
                {
                    for (double v = 0.1; v < 1.0; v += 0.2)
                    {
                        var cells = new double2(tile.I + u, tile.J + v);
                        double2 metres = cells * CourseSize.CellM;
                        ColliderBox[] under = [.. boxes.Where(b => Over(b, metres))];
                        Assert.True(under.Length > 0, $"{piece.Def.Id}: nothing to stand on at {cells}");

                        double felt = CourseLines.FloorAt(piece, cells);
                        Assert.All(under, b => Assert.True(Math.Abs(b.TopAt(metres) - felt) < 1e-9, $"{piece.Def.Id} at {cells}: top {b.TopAt(metres):F4} against felt {felt:F4}"));
                    }
                }
            }
        }
    }

    [Fact]
    public void ABendsEmptyCornerHasNothingToStandOn()
    {
        var bend = new Placed(PieceCatalogue.Find("bend2")!, new Cell(0, 0), Orientation.Identity, 0);

        Assert.DoesNotContain(CourseColliders.Of(new Course([bend])), b => Over(b, new double2(1.5, 0.5)));
    }

    [Fact]
    public void ASlabReachesDownIntoTheGround()
    {
        var tee = new Placed(PieceCatalogue.Find("tee")!, new Cell(0, 0), Orientation.Identity, 1);
        ColliderBox box = CourseColliders.Of(new Course([tee]))[0];

        Assert.True(box.Centre.Z - (box.Size.Z / 2.0) <= -PieceShape.BaseDepthM + 1e-9);
    }
}
