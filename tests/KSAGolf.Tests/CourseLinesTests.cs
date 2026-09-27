using Brutal.Numerics;
using Xunit;

namespace KSAGolf.Tests;

/// <summary>A course drawn as lines, and the sample hole that shows every piece.</summary>
public class CourseLinesTests
{
    [Fact]
    public void TheSampleHoleUsesEveryPieceAndCanBePlayed()
    {
        Course course = SampleCourse.Build();
        CourseReport report = course.Check();

        Assert.True(report.Playable, string.Join("; ", report.Problems));
        Assert.Equal(SampleCourse.Order, course.Pieces.Select(p => p.Def.Id));
    }

    [Fact]
    public void ARampRisesOneLevelFromPortToPort()
    {
        var ramp = new Placed(PieceCatalogue.Find("ramp")!, new Cell(0, 0), Orientation.Identity, 2);

        Assert.Equal(2 * CourseSize.LevelM, CourseLines.FloorAt(ramp, new double2(0.5, 0.0)), 9);
        Assert.Equal(2.5 * CourseSize.LevelM, CourseLines.FloorAt(ramp, new double2(0.5, 1.0)), 9);
        Assert.Equal(3 * CourseSize.LevelM, CourseLines.FloorAt(ramp, new double2(0.5, 2.0)), 9);
    }

    [Fact]
    public void AnOpeningIsTheLanesWidthOnTheCellEdge()
    {
        var straight = new Placed(PieceCatalogue.Find("straight")!, new Cell(3, 4), Orientation.Identity, 0);
        List<CourseLine> openings = [.. CourseLines.Of(new Course([straight])).Where(l => l.Kind == LineKind.Opening)];

        double south = openings.Where(l => l.From.Y == 4.0 * CourseSize.CellM).Sum(l => Vec.Len(l.To - l.From));
        Assert.Equal(CourseSize.LaneM, south, 9);
    }

    [Fact]
    public void APieceTheCheckFaultsIsDrawnAsFaulty()
    {
        var lone = new Placed(PieceCatalogue.Find("straight")!, new Cell(0, 0), Orientation.Identity, 0);

        Assert.Contains(CourseLines.Of(new Course([lone])), l => l.Kind == LineKind.Faulty);
    }

    private static double2 EdgeMiddle(Port p)
    {
        double2 o = p.Side switch { Side.North => new(0, 1), Side.East => new(1, 0), Side.South => new(0, -1), _ => new(-1, 0) };
        return (new double2(p.Cell.I + 0.5, p.Cell.J + 0.5) + (o * 0.5)) * CourseSize.CellM;
    }

    /// <summary>A model laid down by its placement puts its openings where the grid says the ports are.</summary>
    [Fact]
    public void AModelsOpeningsLandOnThePortsWhicheverWayItIsTurned()
    {
        foreach (PieceDef def in PieceCatalogue.All)
        {
            foreach (Orientation o in Orientation.All.Where(t => !t.Mirrored))
            {
                var placed = new Placed(def, new Cell(3, -2), o, 0);
                (double2 x, double2 y, double2 origin) = placed.Placement(o);
                double2[] laid = [.. placed.Ports().Select(EdgeMiddle)];

                foreach (Port port in def.Ports)
                {
                    double2 local = EdgeMiddle(port);
                    double2 at = origin + (x * local.X) + (y * local.Y);
                    Assert.Contains(laid, l => double2.Distance(l, at) < 1e-9);
                }
            }
        }
    }

    [Fact]
    public void ASymmetricPieceMirroredIsDrawnAsATurn()
    {
        PieceDef corner = PieceCatalogue.Find("corner")!;
        Orientation? turn = corner.AsTurn(new Orientation(0, true));

        Assert.NotNull(turn);
        Assert.False(turn.Value.Mirrored);
        Assert.Null(PieceCatalogue.Find("sbend")!.AsTurn(new Orientation(0, true)));
    }

    /// <summary>A mirrored twin, turned, puts its openings where a mirrored piece's ports are.</summary>
    [Fact]
    public void AMirrorTwinsOpeningsLandOnTheMirroredPiecesPorts()
    {
        foreach (PieceDef def in PieceCatalogue.All.Where(d => d.MirrorMesh is not null))
        {
            foreach (Orientation o in Orientation.All.Where(t => t.Mirrored))
            {
                var placed = new Placed(def, new Cell(1, 4), o, 0);
                (double2 x, double2 y, double2 origin) = placed.Placement(o with { Mirrored = false });
                double2[] laid = [.. placed.Ports().Select(EdgeMiddle)];

                foreach (Port port in def.Ports)
                {
                    double2 local = EdgeMiddle(port);
                    local = new double2((def.W * CourseSize.CellM) - local.X, local.Y);
                    double2 at = origin + (x * local.X) + (y * local.Y);
                    Assert.Contains(laid, l => double2.Distance(l, at) < 1e-9);
                }
            }
        }
    }

    /// <summary>A model a piece names but the asset XML does not declare is drawn as nothing, and nothing says so.</summary>
    [Fact]
    public void EveryPiecesModelIsDeclared()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KSAGolf.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string xml = File.ReadAllText(Path.Combine(dir.FullName, "src", "KSAGolf", "KSAGolfCourse.xml"));

        foreach (PieceDef def in PieceCatalogue.All)
        {
            foreach (string? id in new[] { def.Mesh, def.MirrorMesh })
            {
                if (id is not null) Assert.Contains($"<GltfFile Id=\"{id}\">", xml);
            }
        }
    }
}
