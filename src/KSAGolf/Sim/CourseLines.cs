using Brutal.Numerics;

namespace KSAGolf;

internal enum LineKind
{
    /// <summary>A piece's outline.</summary>
    Outline,

    /// <summary>A piece's outline, where the check found something wrong with it.</summary>
    Faulty,

    /// <summary>A cell edge inside a piece.</summary>
    Cell,

    /// <summary>The opening a lane crosses at a port, the lane's width.</summary>
    Opening,

    /// <summary>The line a lane runs along.</summary>
    Lane,

    /// <summary>The tee's mark and the cup's rim.</summary>
    Mark,
}

/// <summary>One straight line to draw, in course-local metres: X along I, Y along J, Z up from the course floor.</summary>
/// <param name="Piece">Which piece of the course it outlines.</param>
internal readonly record struct CourseLine(double3 From, double3 To, LineKind Kind, int Piece = -1);

/// <summary>
/// A course as lines on its grid, before any piece has a mesh: enough to judge its sizes in game, and what
/// the builder draws over the pieces later.
/// </summary>
internal static class CourseLines
{
    /// <summary>A regulation cup's radius.</summary>
    public const double CupRadiusM = 0.054;

    // Short enough that a line over a ramp follows its slope.
    private const double Segment = 0.25;

    public static List<CourseLine> Of(Course course)
    {
        List<CourseLine> lines = [];
        HashSet<int> faulty = [.. course.Check().Problems.Select(p => p.Piece).Where(p => p >= 0)];

        for (int n = 0; n < course.Pieces.Count; n++)
        {
            Placed piece = course.Pieces[n];
            int first = lines.Count;
            (int w, int h) = piece.Footprint;
            double x0 = piece.Origin.I, y0 = piece.Origin.J;
            LineKind edge = faulty.Contains(n) ? LineKind.Faulty : LineKind.Outline;

            // Each tile's four edges: an outline where the piece ends, a cell edge between two of its tiles.
            // The edge between two tiles is drawn from the lower one only.
            HashSet<Cell> tiles = [.. piece.Cells()];
            foreach (Cell tile in tiles)
            {
                double tx = tile.I, ty = tile.J;
                foreach ((Side side, double2 a, double2 b) in (ReadOnlySpan<(Side, double2, double2)>)
                         [(Side.South, new(tx, ty), new(tx + 1, ty)), (Side.East, new(tx + 1, ty), new(tx + 1, ty + 1)),
                          (Side.North, new(tx + 1, ty + 1), new(tx, ty + 1)), (Side.West, new(tx, ty + 1), new(tx, ty))])
                {
                    bool inner = tiles.Contains(tile.Toward(side));
                    if (!inner) Add(lines, piece, a, b, edge);
                    else if (side is Side.North or Side.East) Add(lines, piece, a, b, LineKind.Cell);
                }
            }

            Port[] ports = [.. piece.Ports()];
            foreach (Port port in ports)
            {
                double2 mid = EdgeMiddle(port), along = Along(port.Side) * (CourseSize.LaneM / 2.0 / CourseSize.CellM);
                Add(lines, piece, mid - along, mid + along, LineKind.Opening);
            }

            double2 centre = Centre(new Cell(0, 0)) + new double2(x0, y0) + new double2((w - 1) / 2.0, (h - 1) / 2.0);
            if (ports.Length == 2)
            {
                Lane(lines, piece, [EdgeMiddle(ports[0]), Centre(ports[0].Cell), Centre(ports[1].Cell), EdgeMiddle(ports[1])]);
            }
            else
            {
                Lane(lines, piece, [EdgeMiddle(ports[0]), centre]);
            }

            if (piece.Def.Kind == PieceKind.Cup) Circle(lines, piece, centre, CupRadiusM / CourseSize.CellM);
            if (piece.Def.Kind == PieceKind.Tee)
            {
                double s = 0.1 / CourseSize.CellM;
                Add(lines, piece, centre - new double2(s, 0), centre + new double2(s, 0), LineKind.Mark);
                Add(lines, piece, centre - new double2(0, s), centre + new double2(0, s), LineKind.Mark);
            }

            for (int k = first; k < lines.Count; k++) lines[k] = lines[k] with { Piece = n };
        }

        return lines;
    }

    /// <summary>
    /// The floor's height above the course floor at a point of a piece, in metres: its level, and across
    /// a piece whose ports differ in level, a straight rise from one port to the other.
    /// </summary>
    public static double FloorAt(Placed piece, double2 cells)
    {
        Port[] ports = [.. piece.Ports()];
        if (ports.Length < 2 || ports[0].Level == ports[1].Level) return piece.Level * CourseSize.LevelM;

        double2 a = EdgeMiddle(ports[0]), b = EdgeMiddle(ports[1]), ab = b - a;
        double t = Math.Clamp(double2.Dot(cells - a, ab) / double2.Dot(ab, ab), 0.0, 1.0);
        return (ports[0].Level + (t * (ports[1].Level - ports[0].Level))) * CourseSize.LevelM;
    }

    private static double2 Centre(Cell cell) => new(cell.I + 0.5, cell.J + 0.5);

    private static double2 EdgeMiddle(Port port) => Centre(port.Cell) + (Out(port.Side) * 0.5);

    private static double2 Out(Side side) => side switch
    {
        Side.North => new(0, 1),
        Side.East => new(1, 0),
        Side.South => new(0, -1),
        _ => new(-1, 0),
    };

    private static double2 Along(Side side) => side is Side.North or Side.South ? new(1, 0) : new(0, 1);

    private static void Lane(List<CourseLine> lines, Placed piece, double2[] points)
    {
        for (int i = 0; i + 1 < points.Length; i++)
        {
            if (double2.Distance(points[i], points[i + 1]) > 1e-9) Add(lines, piece, points[i], points[i + 1], LineKind.Lane);
        }
    }

    private static void Circle(List<CourseLine> lines, Placed piece, double2 centre, double radius)
    {
        const int Sides = 24;
        for (int k = 0; k < Sides; k++)
        {
            double a = k * Math.Tau / Sides, b = (k + 1) * Math.Tau / Sides;
            Add(lines, piece, centre + (new double2(Math.Cos(a), Math.Sin(a)) * radius), centre + (new double2(Math.Cos(b), Math.Sin(b)) * radius), LineKind.Mark);
        }
    }

    // In cells, broken into short pieces lifted onto the piece's floor.
    private static void Add(List<CourseLine> lines, Placed piece, double2 from, double2 to, LineKind kind)
    {
        int parts = Math.Max(1, (int)Math.Ceiling(double2.Distance(from, to) * CourseSize.CellM / Segment));
        for (int k = 0; k < parts; k++)
        {
            double2 a = double2.Lerp(from, to, (double)k / parts), b = double2.Lerp(from, to, (double)(k + 1) / parts);
            lines.Add(new CourseLine(Lift(piece, a), Lift(piece, b), kind));
        }
    }

    private static double3 Lift(Placed piece, double2 cells) => new(cells.X * CourseSize.CellM, cells.Y * CourseSize.CellM, FloorAt(piece, cells));
}

/// <summary>A hole to look at before anything can build one: every kind of piece, each snapped onto the last.</summary>
internal static class SampleCourse
{
    public static readonly string[] Order = ["tee", "straight3", "bend2", "straight", "sbend", "ramp", "straight", "corner", "room4", "room2", "cup"];

    public static Course Build()
    {
        Course course = Course.Empty.With(new Placed(PieceCatalogue.Find("tee")!, new Cell(0, 0), Orientation.Identity, 0));
        foreach (string id in Order.Skip(1))
        {
            (int Piece, Port Port)[] open = [.. course.OpenPorts()];
            if (open.Length == 0 || PieceCatalogue.Find(id) is not { } def) break;
            if (course.Snap(def, Orientation.Identity, open[^1].Port.Outside, reach: 0) is { } next) course = course.With(next);
        }

        return course;
    }
}
