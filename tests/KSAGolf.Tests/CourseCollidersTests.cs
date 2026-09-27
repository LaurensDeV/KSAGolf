using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>The boxes a kitten stands on: their tops are the felt the ball rolls on, flat and up a ramp.</summary>
public class CourseCollidersTests
{
    [Fact]
    public void EachSlabsTopIsTheFelt()
    {
        Course course = SampleCourse.Build();
        List<ColliderBox> boxes = CourseColliders.Of(course);
        Assert.Equal(course.Pieces.Count, boxes.Count);

        for (int n = 0; n < course.Pieces.Count; n++)
        {
            Placed piece = course.Pieces[n];
            (int w, int h) = piece.Footprint;
            for (double u = 0.1; u < 1.0; u += 0.2)
            {
                for (double v = 0.1; v < 1.0; v += 0.2)
                {
                    var cells = new double2(piece.Origin.I + (u * w), piece.Origin.J + (v * h));
                    double felt = CourseLines.FloorAt(piece, cells);
                    double top = boxes[n].TopAt(cells * CourseSize.CellM);
                    Assert.True(Math.Abs(top - felt) < 1e-9, $"{piece.Def.Id} at {cells}: top {top:F4} against felt {felt:F4}");
                }
            }
        }
    }

    [Fact]
    public void ASlabReachesDownIntoTheGround()
    {
        var tee = new Placed(PieceCatalogue.Find("tee")!, new Cell(0, 0), Orientation.Identity, 1);
        ColliderBox box = CourseColliders.Of(new Course([tee]))[0];

        Assert.True(box.Centre.Z - (box.Size.Z / 2.0) <= -PieceShape.BaseDepthM + 1e-9);
    }
}
