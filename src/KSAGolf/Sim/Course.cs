using Brutal.Numerics;

namespace KSAGolf;

/// <summary>A piece laid on the grid: which cell its south-west corner sits in, how it is turned, and its floor level.</summary>
internal sealed record Placed(PieceDef Def, Cell Origin, Orientation Orientation, int Level)
{
    public (int W, int H) Footprint => Orientation.Footprint(Def.W, Def.H);

    public IEnumerable<Cell> Cells() => Def.LocalCells().Select(c => Origin + Orientation.Apply(c, Def.W, Def.H));

    public IEnumerable<Port> Ports() => Def.Ports.Select(p =>
        new Port(Origin + Orientation.Apply(p.Cell, Def.W, Def.H), Orientation.Apply(p.Side), Level + p.Level));

    /// <summary>
    /// Where the piece's unturned model lands in course-local metres: the images of its X and Y axes and
    /// of its origin. The orientation must be a turn; see <see cref="PieceDef.AsTurn"/>.
    /// </summary>
    public (double2 X, double2 Y, double2 Origin) Placement(Orientation turn)
    {
        double2 x = new(1, 0), y = new(0, 1), corner = new(0, 0);
        double w = Def.W, h = Def.H;
        for (int q = 0; q < turn.Quarter; q++)
        {
            // A quarter turn anticlockwise about the footprint, which then sits h wide and w high.
            x = new(-x.Y, x.X);
            y = new(-y.Y, y.X);
            corner = new(h - corner.Y, corner.X);
            (w, h) = (h, w);
        }

        double c = CourseSize.CellM;
        return (x, y, new double2((Origin.I + corner.X) * c, (Origin.J + corner.Y) * c));
    }

    /// <summary>A point in the piece's own unturned metres, on the course: mirrored, turned and moved as the piece is laid.</summary>
    public double2 ToCourse(double2 piece)
    {
        double c = CourseSize.CellM, w = Def.W * c, h = Def.H * c;
        double x = Orientation.Mirrored ? w - piece.X : piece.X, y = piece.Y;
        for (int q = 0; q < Orientation.Quarter; q++)
        {
            (x, y) = (h - y, x);
            (w, h) = (h, w);
        }

        return new double2(x + (Origin.I * c), y + (Origin.J * c));
    }

    /// <summary>The inverse of <see cref="ToCourse"/>: a course point in the piece's own unturned metres.</summary>
    public double2 ToPiece(double2 course)
    {
        double c = CourseSize.CellM;
        (int fw, int fh) = Footprint;
        double w = fw * c, h = fh * c;
        double x = course.X - (Origin.I * c), y = course.Y - (Origin.J * c);
        for (int q = 0; q < Orientation.Quarter; q++)
        {
            // Undoing a quarter turn anticlockwise on a footprint now w wide and h high.
            (x, y) = (y, w - x);
            (w, h) = (h, w);
        }

        return new double2(Orientation.Mirrored ? (Def.W * c) - x : x, y);
    }

    public bool Covers(Cell cell)
    {
        (int w, int h) = Footprint;
        Cell d = cell - Origin;
        if (d.I < 0 || d.J < 0 || d.I >= w || d.J >= h) return false;

        return Def.IsRectangle || Def.Covers(Orientation.Undo(d, Def.W, Def.H));
    }

    /// <summary>
    /// Laid down another way with <paramref name="pivot"/> still under the same part of the piece, so the
    /// ghost turns under the cursor instead of jumping about its corner.
    /// </summary>
    public Placed TurnedAbout(Cell pivot, Orientation next)
    {
        Cell local = Orientation.Undo(Covers(pivot) ? pivot - Origin : new Cell(0, 0), Def.W, Def.H);
        Cell anchor = Covers(pivot) ? pivot : Origin;
        return this with { Origin = anchor - next.Apply(local, Def.W, Def.H), Orientation = next };
    }
}

/// <summary>How a ghost would sit if placed where it is.</summary>
internal enum Fit
{
    /// <summary>It covers another piece, or sinks below the course floor.</summary>
    Overlaps,

    /// <summary>A lane runs into a wall: its port faces a piece with no matching port there, or the reverse.</summary>
    Blocked,

    /// <summary>At least one of its ports meets a port of the course.</summary>
    Connects,

    /// <summary>Fits, and touches no lane.</summary>
    Open,
}

/// <summary>Something that stops a course being played, and the piece it is at (-1 for the course as a whole).</summary>
internal readonly record struct Problem(string What, int Piece);

/// <summary>A course checked: what is wrong with it, and when nothing is, the pieces from tee to cup.</summary>
internal sealed record CourseReport(IReadOnlyList<Problem> Problems, IReadOnlyList<int> Chain)
{
    public bool Playable => Problems.Count == 0;
}

/// <summary>
/// One hole: pieces on a grid, joined port to port from a tee to a cup. Immutable, so an edit is a new
/// course and undo keeps the old one.
/// </summary>
internal sealed class Course(IReadOnlyList<Placed> pieces)
{
    public static readonly Course Empty = new([]);

    public IReadOnlyList<Placed> Pieces { get; } = pieces;

    public Course With(Placed piece) => new([.. Pieces, piece]);

    public Course Without(int index) => new([.. Pieces.Where((_, i) => i != index)]);

    public Course Replacing(int index, Placed piece) => new([.. Pieces.Select((p, i) => i == index ? piece : p)]);

    public int PieceAt(Cell cell)
    {
        for (int i = 0; i < Pieces.Count; i++)
        {
            if (Pieces[i].Covers(cell)) return i;
        }

        return -1;
    }

    /// <summary>How <paramref name="ghost"/> would sit, ignoring the piece at <paramref name="ignore"/> -- the one being moved.</summary>
    public Fit Judge(Placed ghost, int ignore = -1)
    {
        if (ghost.Level < 0) return Fit.Overlaps;

        foreach (Cell cell in ghost.Cells())
        {
            int at = PieceAt(cell);
            if (at >= 0 && at != ignore) return Fit.Overlaps;
        }

        bool connects = false;
        foreach (Port port in ghost.Ports())
        {
            int at = PieceAt(port.Outside);
            if (at < 0 || at == ignore) continue;
            if (!Pieces[at].Ports().Any(port.Meets)) return Fit.Blocked;
            connects = true;
        }

        for (int i = 0; i < Pieces.Count; i++)
        {
            if (i == ignore) continue;
            foreach (Port port in Pieces[i].Ports())
            {
                if (ghost.Covers(port.Outside) && !ghost.Ports().Any(port.Meets)) return Fit.Blocked;
            }
        }

        return connects ? Fit.Connects : Fit.Open;
    }

    public CourseReport Check()
    {
        List<Problem> problems = [];
        var links = new List<int>[Pieces.Count];
        for (int i = 0; i < Pieces.Count; i++) links[i] = [];

        Dictionary<Cell, int> owner = [];
        for (int i = 0; i < Pieces.Count; i++)
        {
            if (Pieces[i].Level < 0) problems.Add(new Problem("below the course floor", i));
            foreach (Cell cell in Pieces[i].Cells())
            {
                if (owner.TryGetValue(cell, out int other)) problems.Add(new Problem($"overlaps piece {other + 1}", i));
                else owner[cell] = i;
            }
        }

        for (int i = 0; i < Pieces.Count; i++)
        {
            foreach (Port port in Pieces[i].Ports())
            {
                if (!owner.TryGetValue(port.Outside, out int j) || j == i)
                {
                    problems.Add(new Problem("open port", i));
                }
                else if (Pieces[j].Ports().Any(port.Meets))
                {
                    links[i].Add(j);
                }
                else if (Pieces[j].Ports().Any(q => q.Outside == port.Cell && q.Side == Sides.Opposite(port.Side)))
                {
                    // Both sides of a seam see a level mismatch: say it once.
                    if (i < j) problems.Add(new Problem($"a different level from piece {j + 1}", i));
                }
                else
                {
                    problems.Add(new Problem($"lane runs into the side of piece {j + 1}", i));
                }
            }
        }

        int[] tees = Kinds(PieceKind.Tee), cups = Kinds(PieceKind.Cup);
        if (tees.Length != 1) problems.Add(new Problem(tees.Length == 0 ? "no tee" : $"{tees.Length} tees", -1));
        if (cups.Length != 1) problems.Add(new Problem(cups.Length == 0 ? "no cup" : $"{cups.Length} cups", -1));
        if (tees.Length != 1 || cups.Length != 1) return new CourseReport(problems, []);

        // A lane has two ports and a tee or cup one, so the tee's component is a simple path.
        List<int> chain = [tees[0]];
        for (int at = tees[0], from = -1; ;)
        {
            int next = links[at].FirstOrDefault(n => n != from, -1);
            if (next < 0 || chain.Contains(next)) break;
            chain.Add(next);
            (from, at) = (at, next);
        }

        if (chain[^1] != cups[0]) problems.Add(new Problem("the tee does not reach the cup", -1));
        for (int i = 0; i < Pieces.Count; i++)
        {
            if (!chain.Contains(i) && Pieces[i].Def.Kind == PieceKind.Lane) problems.Add(new Problem("not on the way from tee to cup", i));
        }

        return new CourseReport(problems, problems.Count == 0 ? chain : []);
    }

    private int[] Kinds(PieceKind kind) => [.. Enumerable.Range(0, Pieces.Count).Where(i => Pieces[i].Def.Kind == kind)];

    /// <summary>The ports nothing is connected to yet, as (piece, port).</summary>
    public IEnumerable<(int Piece, Port Port)> OpenPorts()
    {
        for (int i = 0; i < Pieces.Count; i++)
        {
            foreach (Port port in Pieces[i].Ports())
            {
                if (PieceAt(port.Outside) < 0) yield return (i, port);
            }
        }
    }

    /// <summary>
    /// Where a ghost under <paramref name="cursor"/> joins the course, if an open port is within
    /// <paramref name="reach"/> cells: the open port nearest the cursor (ties to the lower piece, then
    /// north, east, south, west), in <paramref name="preferred"/> if that fits there, else the next
    /// orientation that does.
    /// </summary>
    public Placed? Snap(PieceDef def, Orientation preferred, Cell cursor, int reach = 2, int ignore = -1)
    {
        var open = OpenPorts()
            .Where(o => o.Piece != ignore)
            .Select(o => (o.Piece, o.Port, Distance: Math.Max(Math.Abs(o.Port.Outside.I - cursor.I), Math.Abs(o.Port.Outside.J - cursor.J))))
            .Where(o => o.Distance <= reach)
            .OrderBy(o => o.Distance).ThenBy(o => o.Piece).ThenBy(o => (int)o.Port.Side)
            .ToList();
        if (open.Count == 0) return null;

        Orientation[] turns = def.DistinctOrientations();
        int start = Math.Max(0, Array.IndexOf(turns, preferred));
        Port target = open[0].Port;

        for (int k = 0; k < turns.Length; k++)
        {
            Orientation o = turns[(start + k) % turns.Length];
            foreach (Port own in def.Ports)
            {
                if (o.Apply(own.Side) != Sides.Opposite(target.Side)) continue;

                var ghost = new Placed(def, target.Outside - o.Apply(own.Cell, def.W, def.H), o, target.Level - own.Level);
                if (Judge(ghost, ignore) == Fit.Connects) return ghost;
            }
        }

        return null;
    }
}
