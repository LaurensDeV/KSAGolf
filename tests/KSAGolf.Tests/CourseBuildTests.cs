using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>The builder's camera and its ghost: what the cursor points at and what a click would place.</summary>
public class CourseBuildTests
{
    private static PieceDef Def(string id) => PieceCatalogue.Find(id)!;

    [Fact]
    public void PanningFollowsTheScreenWhicheverWayTheViewIsTurned()
    {
        var view = new BuildView { HeightM = 10.0 };
        view.Turn(1);
        view.Pan(0.0, 0.5);

        Assert.Equal(new double2(5.0, 0.0), view.Centre);

        view.Pan(0.5, 0.0);
        Assert.Equal(new double2(5.0, -5.0), view.Centre);
    }

    [Fact]
    public void TheViewTurnsRoundAndBack()
    {
        var view = new BuildView();
        view.Turn(-1);
        Assert.Equal(3, view.Quarter);
        view.Turn(5);
        Assert.Equal(0, view.Quarter);
    }

    [Fact]
    public void ZoomStaysBetweenItsLimits()
    {
        var view = new BuildView();
        view.Zoom(100);
        Assert.Equal(BuildView.LowestM, view.HeightM);
        view.Zoom(-100);
        Assert.Equal(BuildView.HighestM, view.HeightM);
    }

    [Fact]
    public void ARayDownFindsTheCellUnderIt()
    {
        double2? hit = BuildView.OnPlane(new double3(0, 0, 12), Vec.Unit(new double3(2.5, -1.2, -12)), 0.0);

        Assert.NotNull(hit);
        Assert.Equal(new Cell(2, -2), BuildView.CellAt(hit.Value));
        Assert.Null(BuildView.OnPlane(new double3(0, 0, 12), new double3(0, 0, 1), 0.0));
    }

    [Fact]
    public void AGhostAwayFromTheCourseIsHeldByItsEntryTile()
    {
        Ghost ghost = Ghost.At(Course.Empty, Def("room4"), Orientation.Identity, 0, new Cell(10, 10));

        Assert.False(ghost.Snapped);
        Assert.Contains(ghost.Piece.Ports(), p => p.Cell == new Cell(10, 10));
        Assert.True(ghost.Placeable);
    }

    [Fact]
    public void AGhostNearTheCourseSnapsAndCanBePlaced()
    {
        Course course = Course.Empty.With(new Placed(Def("tee"), new Cell(0, 0), Orientation.Identity, 0));

        Ghost ghost = Ghost.At(course, Def("straight"), Orientation.Identity, 0, new Cell(1, 2));

        Assert.True(ghost.Snapped);
        Assert.Equal(Fit.Connects, ghost.Fit);
    }

    [Fact]
    public void AGhostOnTopOfAPieceCannotBePlaced()
    {
        // Both of the room's ports taken, so nothing near the cursor can be snapped onto.
        Course course = new([new Placed(Def("tee"), new Cell(1, -1), Orientation.Identity, 0),
                             new Placed(Def("room4"), new Cell(0, 0), Orientation.Identity, 0),
                             new Placed(Def("straight"), new Cell(2, 4), Orientation.Identity, 0)]);

        Ghost ghost = Ghost.At(course, Def("straight"), new Orientation(1, false), 0, new Cell(0, 1));
        Assert.False(ghost.Snapped);
        Assert.False(ghost.Placeable);
    }

    [Fact]
    public void RStepsThroughOnlyTheOrientationsThatDiffer()
    {
        PieceDef straight = Def("straight");
        Orientation next = Ghost.Next(straight, Orientation.Identity);

        Assert.Equal(new Orientation(1, false), next);
        Assert.Equal(Orientation.Identity, Ghost.Next(straight, next));
    }

    [Fact]
    public void MFlipsAnSBendAndLeavesAStraightAlone()
    {
        Assert.Equal(new Orientation(0, true), Ghost.Mirrored(Def("sbend"), Orientation.Identity));
        Assert.Equal(Orientation.Identity, Ghost.Mirrored(Def("straight"), Orientation.Identity));
    }
}
