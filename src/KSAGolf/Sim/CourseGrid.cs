namespace KSAGolf;

/// <summary>A cell edge, named by the way it faces: north is +J, east is +I.</summary>
internal enum Side
{
    North,
    East,
    South,
    West,
}

/// <summary>One square of a course's grid: I counts east, J counts north.</summary>
internal readonly record struct Cell(int I, int J)
{
    public Cell Toward(Side side) => side switch
    {
        Side.North => this with { J = J + 1 },
        Side.East => this with { I = I + 1 },
        Side.South => this with { J = J - 1 },
        _ => this with { I = I - 1 },
    };

    public static Cell operator +(Cell a, Cell b) => new(a.I + b.I, a.J + b.J);

    public static Cell operator -(Cell a, Cell b) => new(a.I - b.I, a.J - b.J);
}

/// <summary>
/// Where a lane leaves a piece: the cell edge it crosses, and the floor level it crosses at.
///
/// <para>An edge rather than a tile, because a tile does not say which way the lane leaves: a corner
/// cell has two outward sides and a 1×1 piece has four, and a 1×1 straight is entered and left through
/// the same tile.</para>
/// </summary>
internal readonly record struct Port(Cell Cell, Side Side, int Level)
{
    /// <summary>The cell a connecting piece's port has to sit in.</summary>
    public Cell Outside => Cell.Toward(Side);

    /// <summary>Whether a lane runs straight from this port into that one.</summary>
    public bool Meets(Port other) => other.Cell == Outside && other.Side == Sides.Opposite(Side) && other.Level == Level;
}

internal static class Sides
{
    public static readonly Side[] All = [Side.North, Side.East, Side.South, Side.West];

    public static Side Opposite(Side side) => (Side)(((int)side + 2) % 4);

    /// <summary>A quarter turn anticlockwise, seen from above.</summary>
    public static Side TurnLeft(Side side) => (Side)(((int)side + 3) % 4);

    /// <summary>East and west swapped, as a mirror across the north-south axis swaps them.</summary>
    public static Side Mirror(Side side) => side switch
    {
        Side.East => Side.West,
        Side.West => Side.East,
        _ => side,
    };
}

/// <summary>How a piece is laid down: mirrored east-west first, then turned anticlockwise in quarters.</summary>
internal readonly record struct Orientation(int Turns, bool Mirrored)
{
    public static readonly Orientation Identity = new(0, false);

    /// <summary>All eight, in the order R steps through them.</summary>
    public static readonly Orientation[] All =
        [new(0, false), new(1, false), new(2, false), new(3, false), new(0, true), new(1, true), new(2, true), new(3, true)];

    public int Quarter => ((Turns % 4) + 4) % 4;

    /// <summary>The footprint once laid down: a quarter turn swaps its width and height.</summary>
    public (int W, int H) Footprint(int w, int h) => Quarter % 2 == 0 ? (w, h) : (h, w);

    /// <summary>A cell of a <paramref name="w"/> × <paramref name="h"/> footprint, laid down, counted from its south-west corner.</summary>
    public Cell Apply(Cell local, int w, int h)
    {
        int x = Mirrored ? w - 1 - local.I : local.I, y = local.J;
        for (int q = 0; q < Quarter; q++)
        {
            (x, y) = (h - 1 - y, x);
            (w, h) = (h, w);
        }

        return new Cell(x, y);
    }

    public Side Apply(Side side)
    {
        side = Mirrored ? Sides.Mirror(side) : side;
        for (int q = 0; q < Quarter; q++) side = Sides.TurnLeft(side);
        return side;
    }

    /// <summary>The local cell that <see cref="Apply(Cell, int, int)"/> carries onto <paramref name="laid"/>.</summary>
    public Cell Undo(Cell laid, int w, int h)
    {
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (Apply(new Cell(i, j), w, h) == laid) return new Cell(i, j);
            }
        }

        return laid;
    }
}
