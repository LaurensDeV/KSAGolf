using Xunit;

namespace KSAGolf.Tests;

/// <summary>The grid a course is built on: laying pieces down turned and mirrored, joining them port to port, and checking the hole.</summary>
public class CourseTests
{
    private static PieceDef Def(string id) => PieceCatalogue.Find(id)!;

    private static Placed Put(string id, int i, int j, int turns = 0, bool mirrored = false, int level = 0)
        => new(Def(id), new Cell(i, j), new Orientation(turns, mirrored), level);

    private static string Ports(Placed p) => string.Join(" ", p.Ports().Select(q => $"({q.Cell.I},{q.Cell.J}){q.Side.ToString()[0]}"));

    [Fact]
    public void EveryPieceInTheCatalogueIsWellFormed()
    {
        foreach (PieceDef def in PieceCatalogue.All) Assert.Null(def.Fault());
        Assert.Equal(PieceCatalogue.All.Length, PieceCatalogue.All.Select(d => d.Id).Distinct().Count());
    }

    [Fact]
    public void APortFacingIntoItsOwnPieceIsRefused()
    {
        var def = new PieceDef("bad", "Bad", PieceKind.Lane, 4, 4, [new(new Cell(1, 1), Side.North, 0), new(new Cell(0, 0), Side.South, 0)]);
        Assert.NotNull(def.Fault());
    }

    /// <summary>The S-bend, 2×3 with ports (0,0)S and (1,2)N, laid down each way.</summary>
    [Fact]
    public void AnSBendTurnsAndMirrorsOntoTheRightEdges()
    {
        Assert.Equal("(0,0)S (1,2)N", Ports(Put("sbend", 0, 0)));
        Assert.Equal("(2,0)E (0,1)W", Ports(Put("sbend", 0, 0, turns: 1)));
        Assert.Equal("(1,0)S (0,2)N", Ports(Put("sbend", 0, 0, mirrored: true)));
        Assert.Equal("(2,1)E (0,0)W", Ports(Put("sbend", 0, 0, turns: 1, mirrored: true)));
        Assert.Equal((3, 2), Put("sbend", 0, 0, turns: 1).Footprint);
    }

    [Fact]
    public void EveryCellOfAPieceIsCoveredOnceWhicheverWayItLies()
    {
        foreach (PieceDef def in PieceCatalogue.All)
        {
            foreach (Orientation o in Orientation.All)
            {
                var p = new Placed(def, new Cell(5, -3), o, 0);
                List<Cell> cells = [.. p.Cells()];
                Assert.Equal(def.LocalCells().Count(), cells.Distinct().Count());
                Assert.All(cells, c => Assert.True(p.Covers(c)));
                Assert.All(p.Ports(), q => Assert.True(p.Covers(q.Cell) && !p.Covers(q.Outside)));
            }
        }
    }

    [Fact]
    public void OnlyTheOrientationsThatDifferAreOffered()
    {
        Assert.Equal(2, Def("straight").DistinctOrientations().Length);
        Assert.Equal(2, Def("straight3").DistinctOrientations().Length);
        Assert.Equal(4, Def("corner").DistinctOrientations().Length);
        Assert.Equal(4, Def("sbend").DistinctOrientations().Length);
        Assert.Equal(4, Def("ramp").DistinctOrientations().Length);
        Assert.Equal(4, Def("tee").DistinctOrientations().Length);
    }

    /// <summary>A 2×3 piece turned about its corner would jump by a cell; turned about the cursor it stays under it.</summary>
    [Fact]
    public void TurningAGhostKeepsTheCellUnderTheCursorCovered()
    {
        Placed ghost = Put("sbend", 5, 5);
        var cursor = new Cell(6, 7);
        foreach (Orientation o in Orientation.All)
        {
            Placed turned = ghost.TurnedAbout(cursor, o);
            Assert.True(turned.Covers(cursor), $"{o} left the cursor");
            Assert.Equal(o, turned.Orientation);
        }
    }

    private static Course Hole(params Placed[] pieces) => new(pieces);

    [Fact]
    public void ATeeBesideTheCupIsAHole()
    {
        CourseReport report = Hole(Put("tee", 0, 0), Put("cup", 0, 1)).Check();

        Assert.True(report.Playable, string.Join("; ", report.Problems));
        Assert.Equal(new[] { 0, 1 }, report.Chain);
    }

    [Fact]
    public void AChainRoundACornerIsAHole()
    {
        CourseReport report = Hole(Put("tee", 0, 0), Put("straight3", 0, 1), Put("corner", 0, 4), Put("cup", 1, 4, turns: 3)).Check();

        Assert.True(report.Playable, string.Join("; ", report.Problems));
        Assert.Equal(new[] { 0, 1, 2, 3 }, report.Chain);
    }

    [Fact]
    public void EitherEndOfALaneIsItsEntry()
    {
        // The long straight turned end over end: its "exit" now faces the tee.
        CourseReport report = Hole(Put("tee", 0, 0), Put("straight3", 0, 1, turns: 2), Put("cup", 0, 4)).Check();

        Assert.True(report.Playable, string.Join("; ", report.Problems));
    }

    [Fact]
    public void AClosedLoopBesideAHoleIsNotPartOfIt()
    {
        // Four corners closing on themselves use every port, so only belonging to the chain catches them.
        CourseReport report = Hole(
            Put("tee", 0, 0), Put("cup", 0, 1),
            Put("corner", 5, 5, turns: 1), Put("corner", 5, 6), Put("corner", 6, 6, turns: 3), Put("corner", 6, 5, turns: 2)).Check();

        Assert.False(report.Playable);
        Assert.Equal(new[] { 2, 3, 4, 5 }, report.Problems.Where(p => p.What.Contains("not on the way")).Select(p => p.Piece).Order());
    }

    [Fact]
    public void ALaneIntoTheSideOfAPieceIsBlocked()
    {
        // The tee's lane runs into the long side of a straight lying east-west.
        CourseReport report = Hole(Put("tee", 0, 0), Put("straight3", 0, 1, turns: 1), Put("cup", 5, 5)).Check();

        Assert.Contains(report.Problems, p => p.Piece == 0 && p.What.Contains("side of piece 2"));
    }

    [Fact]
    public void PiecesSideBySideAreNotJoined()
    {
        CourseReport report = Hole(Put("tee", 0, 0), Put("straight", 0, 1), Put("straight", 1, 1), Put("cup", 0, 2)).Check();

        Assert.Contains(report.Problems, p => p.Piece == 2 && p.What == "open port");
        Assert.DoesNotContain(report.Problems, p => p.Piece == 1);
    }

    [Fact]
    public void AfterARampTheCourseCarriesOnALevelUp()
    {
        Assert.True(Hole(Put("tee", 0, 0), Put("ramp", 0, 1), Put("straight", 0, 3, level: 1), Put("cup", 0, 4, level: 1)).Check().Playable);

        CourseReport flat = Hole(Put("tee", 0, 0), Put("ramp", 0, 1), Put("straight", 0, 3), Put("cup", 0, 4)).Check();
        Assert.Single(flat.Problems, p => p.What.Contains("different level"));
    }

    [Fact]
    public void AHoleNeedsOneTeeAndOneCup()
    {
        Assert.Contains(Hole(Put("straight", 0, 0)).Check().Problems, p => p.What == "no tee");
        Assert.Contains(Hole(Put("tee", 0, 0), Put("cup", 0, 1), Put("cup", 3, 3)).Check().Problems, p => p.What == "2 cups");
    }

    [Fact]
    public void OverlappingPiecesAreReported()
    {
        Assert.Contains(Hole(Put("tee", 0, 0), Put("room2", 0, 0), Put("cup", 0, 5)).Check().Problems, p => p.What.StartsWith("overlaps"));
    }

    [Fact]
    public void AGhostIsJudgedByWhatItWouldTouch()
    {
        Course course = Hole(Put("tee", 0, 0));

        Assert.Equal(Fit.Connects, course.Judge(Put("straight", 0, 1)));
        Assert.Equal(Fit.Overlaps, course.Judge(Put("straight", 0, 0)));
        Assert.Equal(Fit.Blocked, course.Judge(Put("straight", 0, 1, turns: 1)));
        Assert.Equal(Fit.Open, course.Judge(Put("straight", 4, 4)));
        Assert.Equal(Fit.Overlaps, course.Judge(Put("straight", 4, 4, level: -1)));
    }

    [Fact]
    public void AMovedPieceDoesNotCollideWithItself()
    {
        Course course = Hole(Put("tee", 0, 0), Put("room2", 0, 1));

        Assert.Equal(Fit.Connects, course.Judge(Put("room2", 0, 1), ignore: 1));
    }

    [Fact]
    public void AGhostNearAnOpenPortSnapsOnToIt()
    {
        Course course = Hole(Put("tee", 0, 0));

        Placed? snapped = course.Snap(Def("sbend"), Orientation.Identity, new Cell(1, 2));
        Assert.NotNull(snapped);
        Assert.Equal(Fit.Connects, course.Judge(snapped));
        Assert.Contains(snapped.Ports(), p => p.Cell == new Cell(0, 1) && p.Side == Side.South);
    }

    [Fact]
    public void ASnapKeepsTheOrientationAskedForWhenItFits()
    {
        Course course = Hole(Put("tee", 0, 0));
        Orientation mirrored = new(0, true);

        Assert.Equal(mirrored, course.Snap(Def("sbend"), mirrored, new Cell(0, 1))!.Orientation);
    }

    [Fact]
    public void ASnapUpARampLandsALevelUp()
    {
        Course course = Hole(Put("tee", 0, 0), Put("ramp", 0, 1));

        Assert.Equal(1, course.Snap(Def("straight"), Orientation.Identity, new Cell(0, 3))!.Level);
    }

    [Fact]
    public void NothingSnapsFarFromAnOpenPort()
    {
        Assert.Null(Hole(Put("tee", 0, 0)).Snap(Def("straight"), Orientation.Identity, new Cell(10, 10)));
    }

    [Fact]
    public void UndoAndRedoStepThroughEdits()
    {
        var edits = new CourseEdits(Course.Empty);
        edits.Apply(edits.Current.With(Put("tee", 0, 0)));
        edits.Apply(edits.Current.With(Put("cup", 0, 1)));

        Assert.True(edits.Undo());
        Assert.Single(edits.Current.Pieces);
        Assert.True(edits.Redo());
        Assert.Equal(2, edits.Current.Pieces.Count);

        edits.Undo();
        edits.Apply(edits.Current.Without(0));
        Assert.False(edits.CanRedo);
        Assert.Empty(edits.Current.Pieces);
    }

    [Fact]
    public void ACourseSurvivesASaveAndLoad()
    {
        Course course = Hole(Put("tee", 0, 0), Put("ramp", 0, 1), Put("sbend", -1, 3, turns: 3, mirrored: true, level: 1));
        var where = new CourseFile { Name = "First", Body = "Earth", LatitudeDeg = 28.6, LongitudeDeg = -80.6, HeadingDeg = 90, FloorAboveGroundM = 0.03 };

        CourseFile? read = CourseFile.FromJson(CourseFile.Of(course, where).ToJson(), out string why);
        Assert.NotNull(read);
        Course back = read.ToCourse(out List<string> unknown);

        Assert.Empty(unknown);
        Assert.Equal("Earth", read.Body);
        Assert.Equal(course.Pieces, back.Pieces, (a, b) => a.Def == b.Def && a.Origin == b.Origin && a.Orientation == b.Orientation && a.Level == b.Level);
    }

    [Fact]
    public void APieceThisBuildDoesNotKnowIsNamedNotThrown()
    {
        var file = new CourseFile { Pieces = [new("windmill", 0, 0, 0, false, 0), new("tee", 0, 1, 0, false, 0)] };

        Course course = file.ToCourse(out List<string> unknown);

        Assert.Equal(new[] { "windmill" }, unknown);
        Assert.Single(course.Pieces);
    }

    [Fact]
    public void ACourseFromANewerBuildIsRefused()
    {
        Assert.Null(CourseFile.FromJson("{\"version\": 99}", out string why));
        Assert.Contains("newer", why);
    }

    /// <summary>A bend takes up the cells its lane crosses; the corner it cuts across is free for another piece.</summary>
    [Fact]
    public void ABendLeavesTheCornerItCutsFree()
    {
        Course course = Hole(Put("bend2", 0, 0));

        Assert.Equal(-1, course.PieceAt(new Cell(1, 0)));
        Assert.Equal(0, course.PieceAt(new Cell(1, 1)));
        Assert.NotEqual(Fit.Overlaps, course.Judge(Put("straight", 1, 0)));
    }

    [Fact]
    public void AShapedPieceTurnsWithItsShape()
    {
        foreach (Orientation o in Orientation.All)
        {
            var bend = new Placed(Def("bend2"), new Cell(0, 0), o, 0);
            Assert.Equal(3, bend.Cells().Count());
            Assert.All(bend.Ports(), p => Assert.True(bend.Covers(p.Cell), $"{o}: a port on a cell the bend does not take up"));
        }
    }
}
