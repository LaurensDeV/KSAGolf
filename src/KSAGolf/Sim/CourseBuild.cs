using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// The builder's camera, in course-local metres: straight down over a point of the course, at a height,
/// turned in quarters so north need not be up the screen.
/// </summary>
internal sealed class BuildView
{
    public const double LowestM = 3.0;
    public const double HighestM = 60.0;

    public double2 Centre;
    public double HeightM = 12.0;

    /// <summary>Quarter turns of the picture: the course's +J is up the screen at 0, and its +I at 1.</summary>
    public int Quarter;

    /// <summary>The course-local direction that is up the screen.</summary>
    public double2 ScreenUp => Quarter switch
    {
        0 => new(0, 1),
        1 => new(1, 0),
        2 => new(0, -1),
        _ => new(-1, 0),
    };

    /// <summary>The course-local direction that is right on the screen, seen from above.</summary>
    public double2 ScreenRight => new(ScreenUp.Y, -ScreenUp.X);

    /// <summary>Moves the view by so many screen-heights up and screen-widths right, scaled by how high it is.</summary>
    public void Pan(double right, double up) => Centre += ((ScreenRight * right) + (ScreenUp * up)) * HeightM;

    /// <summary>Wheel steps: each goes a sixth closer or further.</summary>
    public void Zoom(double steps) => HeightM = Math.Clamp(HeightM * Math.Pow(1.0 / 1.2, steps), LowestM, HighestM);

    public void Turn(int quarters) => Quarter = (((Quarter + quarters) % 4) + 4) % 4;

    /// <summary>
    /// Where a ray meets the course's plane at <paramref name="heightM"/>, in course-local metres, or null
    /// when it runs parallel or away. Origin and direction are course-local, Z up.
    /// </summary>
    public static double2? OnPlane(double3 origin, double3 direction, double heightM)
    {
        if (Math.Abs(direction.Z) < 1e-9) return null;

        double t = (heightM - origin.Z) / direction.Z;
        return t > 0.0 ? new double2(origin.X + (direction.X * t), origin.Y + (direction.Y * t)) : null;
    }

    public static Cell CellAt(double2 metres) => new((int)Math.Floor(metres.X / CourseSize.CellM), (int)Math.Floor(metres.Y / CourseSize.CellM));
}

/// <summary>A ghost as the builder shows it: where it would go, how it would sit, and whether it snapped there.</summary>
internal readonly record struct Ghost(Placed Piece, Fit Fit, bool Snapped)
{
    public bool Placeable => Fit is Fit.Connects or Fit.Open;

    /// <summary>
    /// The ghost of <paramref name="def"/> under <paramref name="cursor"/>: snapped onto a nearby open port if
    /// one takes it, else held by its first port's tile at the cursor, at the level asked for.
    /// </summary>
    public static Ghost At(Course course, PieceDef def, Orientation orientation, int level, Cell cursor)
    {
        if (course.Snap(def, orientation, cursor) is { } snapped) return new Ghost(snapped, course.Judge(snapped), true);

        var free = new Placed(def, cursor - orientation.Apply(def.Ports[0].Cell, def.W, def.H), orientation, level);
        return new Ghost(free, course.Judge(free), false);
    }

    /// <summary>The orientation R steps to: the next that lays the piece down differently.</summary>
    public static Orientation Next(PieceDef def, Orientation current)
    {
        Orientation[] turns = def.DistinctOrientations();
        int at = Array.IndexOf(turns, current);
        return turns[(at + 1) % turns.Length];
    }

    /// <summary>The mirror image, if the piece has one that differs, else as it is.</summary>
    public static Orientation Mirrored(PieceDef def, Orientation current)
    {
        Orientation flipped = current with { Mirrored = !current.Mirrored };
        Orientation[] turns = def.DistinctOrientations();
        if (Array.IndexOf(turns, flipped) >= 0) return flipped;

        // A piece whose mirror is one of its turns: show that turn, so M still visibly flips it.
        (int w, int h) = flipped.Footprint(def.W, def.H);
        string Key(Orientation o) => string.Join(",", def.Ports.Select(p => $"{o.Apply(p.Cell, def.W, def.H)}{o.Apply(p.Side)}{p.Level}").Order());
        return turns.FirstOrDefault(o => o.Footprint(def.W, def.H) == (w, h) && Key(o) == Key(flipped), current);
    }
}
