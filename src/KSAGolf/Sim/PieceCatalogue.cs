namespace KSAGolf;

internal enum PieceKind
{
    Tee,
    Cup,
    Lane,
}

/// <summary>
/// A kind of course piece: its footprint in cells and where its lane leaves it, laid down unturned.
/// A tee and a cup have one port, a lane two, and a lane is played from either end.
/// </summary>
/// <param name="Id">Named by saved courses, so never renamed once shipped.</param>
/// <param name="Mesh">The <c>&lt;GltfFile&gt;</c> drawn for it, modelled unturned; null while it is drawn as lines.</param>
/// <param name="MirrorMesh">Its mirror image, for a piece whose mirror is not one of its turns.</param>
/// <param name="Tiles">
/// The cells it takes up, where that is not its whole <paramref name="W"/> × <paramref name="H"/>: a bend is an L,
/// and the cells its lane never crosses are left free for another piece.
/// </param>
internal sealed record PieceDef(string Id, string Name, PieceKind Kind, int W, int H, Port[] Ports, string? Mesh = null, string? MirrorMesh = null,
                                Cell[]? Tiles = null)
{
    /// <summary>The cells it takes up, unturned.</summary>
    public IEnumerable<Cell> LocalCells()
    {
        if (Tiles is not null)
        {
            foreach (Cell tile in Tiles) yield return tile;
            yield break;
        }

        for (int i = 0; i < W; i++)
        {
            for (int j = 0; j < H; j++) yield return new Cell(i, j);
        }
    }

    public bool IsRectangle => Tiles is null;

    /// <summary>What is wrong with the definition itself, or null.</summary>
    public string? Fault()
    {
        if (W < 1 || H < 1) return $"{Id}: footprint {W}x{H}";
        if (Tiles is not null && Tiles.Any(t => t.I < 0 || t.J < 0 || t.I >= W || t.J >= H)) return $"{Id}: a tile outside its {W}x{H}";
        if (Ports.Length != (Kind == PieceKind.Lane ? 2 : 1)) return $"{Id}: {Ports.Length} ports for a {Kind}";

        foreach (Port port in Ports)
        {
            if (!Covers(port.Cell)) return $"{Id}: port cell {port.Cell} outside the footprint";
            if (Covers(port.Outside)) return $"{Id}: port at {port.Cell} {port.Side} faces into the piece";
        }

        return Ports.Length == 2 && Ports[0].Cell == Ports[1].Cell && Ports[0].Side == Ports[1].Side ? $"{Id}: both ports on one edge" : null;
    }

    /// <summary>
    /// The unmirrored orientation that lays the piece down exactly as <paramref name="o"/> does, if there is
    /// one: a mesh is drawn turned but never mirrored, since a mirror turns its triangles inside out.
    /// </summary>
    public Orientation? AsTurn(Orientation o)
    {
        if (!o.Mirrored) return o;

        string wanted = Key(o);
        foreach (Orientation turn in Orientation.All.Where(t => !t.Mirrored))
        {
            if (turn.Footprint(W, H) == o.Footprint(W, H) && Key(turn) == wanted) return turn;
        }

        return null;
    }

    // How a piece lies once laid, whatever orientation got it there: its ports and the cells it takes up.
    private string Key(Orientation o)
        => string.Join(",", Ports.Select(p => $"{o.Apply(p.Cell, W, H)}{o.Apply(p.Side)}{p.Level}").Order())
           + "|" + string.Join(",", LocalCells().Select(c => o.Apply(c, W, H).ToString()).Order());

    /// <summary>Whether it takes up a cell of its own, unturned.</summary>
    public bool Covers(Cell local)
        => local.I >= 0 && local.J >= 0 && local.I < W && local.J < H && (Tiles is null || Array.IndexOf(Tiles, local) >= 0);

    /// <summary>
    /// The orientations that lay the piece down differently, in the order of <see cref="Orientation.All"/>:
    /// a straight has two, an S-bend four. R steps through these only, or it would seem to do nothing.
    /// </summary>
    public Orientation[] DistinctOrientations()
    {
        List<Orientation> kept = [];
        HashSet<string> seen = [];
        foreach (Orientation o in Orientation.All)
        {
            (int w, int h) = o.Footprint(W, H);
            if (seen.Add($"{w}x{h}:{Key(o)}")) kept.Add(o);
        }

        return [.. kept];
    }
}

/// <summary>The pieces a course is built from. Ids are append-only: a saved course names them.</summary>
internal static class PieceCatalogue
{
    private static Port P(int i, int j, Side side, int level = 0) => new(new Cell(i, j), side, level);

    public static readonly PieceDef[] All =
    [
        new("tee", "Tee", PieceKind.Tee, 1, 1, [P(0, 0, Side.North)], "KSAGolf_Piece_Tee_Glb"),
        new("cup", "Cup", PieceKind.Cup, 1, 1, [P(0, 0, Side.South)], "KSAGolf_Piece_Cup_Glb"),
        new("straight", "Straight", PieceKind.Lane, 1, 1, [P(0, 0, Side.South), P(0, 0, Side.North)], "KSAGolf_Piece_Straight_Glb"),
        new("straight3", "Long straight", PieceKind.Lane, 1, 3, [P(0, 0, Side.South), P(0, 2, Side.North)], "KSAGolf_Piece_Straight3_Glb"),
        new("corner", "Corner", PieceKind.Lane, 1, 1, [P(0, 0, Side.South), P(0, 0, Side.East)], "KSAGolf_Piece_Corner_Glb"),
        new("bend2", "Wide bend", PieceKind.Lane, 2, 2, [P(0, 0, Side.South), P(1, 1, Side.East)], "KSAGolf_Piece_Bend2_Glb", null,
            [new(0, 0), new(0, 1), new(1, 1)]),
        new("sbend", "S-bend", PieceKind.Lane, 2, 3, [P(0, 0, Side.South), P(1, 2, Side.North)], "KSAGolf_Piece_SBend_Glb", "KSAGolf_Piece_SBendMirror_Glb",
            [new(0, 0), new(0, 1), new(1, 1), new(1, 2)]),
        new("ramp", "Ramp", PieceKind.Lane, 1, 2, [P(0, 0, Side.South), P(0, 1, Side.North, 1)], "KSAGolf_Piece_Ramp_Glb"),
        new("room2", "Small room", PieceKind.Lane, 2, 2, [P(0, 0, Side.South), P(1, 1, Side.North)], "KSAGolf_Piece_Room2_Glb"),
        new("room4", "Bumper room", PieceKind.Lane, 4, 4, [P(1, 0, Side.South), P(2, 3, Side.North)], "KSAGolf_Piece_Room4_Glb"),
    ];

    public static PieceDef? Find(string id) => All.FirstOrDefault(d => d.Id == id);
}
