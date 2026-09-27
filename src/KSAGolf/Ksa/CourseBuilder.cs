using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Building a minigolf hole from above: the view straight down over the course, a ghost of the chosen piece
/// under the cursor snapping onto open ports, and the pieces drawn as their models with the grid in lines over them.
///
/// <para>The course is level and body-fixed. Its frame is laid at the kitten when building starts: the grid's
/// +J the way the kitten faces, its first cell a metre ahead. The floor stands just above the highest ground
/// under any cell corner, launch-site aprons included, and is found again after every edit.</para>
///
/// <para>While building, the view takes the keyboard and mouse from the game (<see cref="IViewPose.TakesInput"/>)
/// and reads them through ImGui, which sees them first. Golf is suspended, so it neither borrows the view nor
/// reads its keys, and its own view is handed back before this one remembers the player's.</para>
/// </summary>
internal sealed class CourseBuilder(Golf golf) : IViewPose
{
    private const double FovDeg = 50.0;
    private const double FloorAboveGroundM = 0.02;

    // How far the builder's grid reaches round the view's centre, in cells.
    private const int GridReach = 12;

    private static readonly float4 White = new(0.95f, 0.95f, 0.95f, 1f);
    private static readonly float4 Red = new(1f, 0.25f, 0.2f, 1f);
    private static readonly float4 Grey = new(0.55f, 0.55f, 0.55f, 1f);
    private static readonly float4 Dim = new(0.3f, 0.3f, 0.3f, 1f);
    private static readonly float4 Green = new(0.3f, 1f, 0.35f, 1f);
    private static readonly float4 Amber = new(1f, 0.7f, 0.15f, 1f);
    private static readonly float4 Yellow = new(1f, 0.85f, 0.2f, 1f);
    private static readonly float4 Cyan = new(0.3f, 0.9f, 1f, 1f);

    private const string Material = "KSAGolf_Course_Material";

    private readonly BuildView _view = new();

    // One renderable per kind of piece, drawn once per placed piece: built on first use and kept, since
    // disposing a renderable drops the handle cache every other renderable of that mesh shares.
    private readonly Dictionary<string, StaticMeshRenderable?> _meshes = [];
    private CourseEdits _edits = new(Course.Empty);

    private Celestial? _body;
    private double3 _anchorCcf, _upCcf, _iCcf, _jCcf;
    private double _floorRadius, _groundAtAnchor;

    private KsaWorld.MainView _saved;
    private bool _borrowed;

    private Orientation _orientation = Orientation.Identity;
    private int _level;
    private Cell? _cursor;
    private Cell? _aimed;
    private Ghost? _ghost;
    private List<CourseLine> _courseLines = [];

    /// <summary>The piece being laid, or -1 for picking pieces up to move them.</summary>
    public int Selected { get; private set; }

    public bool Active { get; private set; }

    public string Name { get; private set; } = "";

    public Course Course => _edits.Current;

    public CourseReport Report { get; private set; } = Course.Empty.Check();

    public string Status { get; private set; } = "";

    public Orientation Orientation => _orientation;

    public int Level => _level;

    public bool CanUndo => _edits.CanUndo;

    public bool CanRedo => _edits.CanRedo;

    public IMouseDrag? Orbit => null;

    public bool UpIsExact => true;

    public bool TakesInput => Active;

    // ---- entering and leaving ---------------------------------------------------------------------

    public void Toggle()
    {
        if (Active) Leave();
        else Enter();
    }

    public void Enter()
    {
        if (Active) return;
        if (golf.Kitten is not { } kitten || kitten.Parent is not Celestial body)
        {
            Status = "fly a kitten on the ground to build a course";
            return;
        }

        golf.Suspended = true;
        if (!ReferenceEquals(body, _body) || !Near(kitten)) AnchorAt(kitten, body);

        kitten.ClearHeldPlayerInput();
        _saved = KsaWorld.RememberMainView();
        _borrowed = true;
        Active = true;
        Refresh();
        Log.Info("course builder: on");
    }

    public void Leave()
    {
        if (!Active) return;

        Active = false;
        if (_borrowed) KsaWorld.TryHandBackMainView(_saved, golf.Kitten, out _, out _);
        _borrowed = false;
        golf.Suspended = false;
        _ghost = null;
        Log.Info("course builder: off");
    }

    /// <summary>Drops the course and its frame, for a world a save load has replaced.</summary>
    public void Forget()
    {
        Leave();
        _body = null;
        _edits = new CourseEdits(Course.Empty);
        _courseLines = [];
        Name = "";
        golf.Course = null;
        CourseCollider.Want(null, []);
    }

    private bool Near(KittenEva kitten)
        => _body is { } body && Vec.Len(ToCcf(body, KsaWorld.PositionEcl(kitten)) - _anchorCcf) < 200.0;

    private void AnchorAt(KittenEva kitten, Celestial body)
    {
        double3 feet = ToCcf(body, KittenFrame.ToEcl(kitten, Vec.Zero));
        double3 up = Vec.Unit(feet);
        double3 facing = Vec.RejectFrom(double3.Transform(KittenFrame.DirectionToEcl(kitten, new double3(0, 0, 1)), body.GetCce2Ccf()), up);
        if (!(Vec.Len2(facing) > 1e-12)) facing = Vec.AnyPerpendicular(up);

        double3 j = Vec.Unit(facing);
        double3 i = Vec.Unit(Vec.Cross(j, up));

        // The first cell a metre ahead, centred on the kitten's line.
        Frame(body, feet + (j * 1.0) - (i * (CourseSize.CellM / 2.0)), i, j);
        _edits = new CourseEdits(Course.Empty);
        _view.Centre = new double2(0.5, 3.0);
        Name = "";
        Changed();
    }

    private void Frame(Celestial body, double3 anchorCcf, double3 i, double3 j)
    {
        _body = body;
        _upCcf = Vec.Unit(anchorCcf);
        _iCcf = Vec.Unit(Vec.RejectFrom(i, _upCcf));
        _jCcf = Vec.Unit(Vec.Cross(_upCcf, _iCcf));
        _groundAtAnchor = new Golf.BodyGround(body).TrySurfaceRadius(_upCcf, out double r) ? r : Vec.Len(anchorCcf);
        _anchorCcf = _upCcf * _groundAtAnchor;
        _floorRadius = _groundAtAnchor + FloorAboveGroundM;
    }

    // ---- the frame's step: the view ---------------------------------------------------------------

    /// <summary>Points the main view, once a frame, from the step.</summary>
    public void Step()
    {
        if (!Active) return;

        if (golf.Kitten is null)
        {
            Leave();
            return;
        }

        if (TryLook(out double3 eyeEcl, out double3 forward, out double3 up) && golf.Kitten is { } kitten)
        {
            KsaWorld.TryLookFromMainViewport(eyeEcl - KsaWorld.PositionEcl(kitten), forward, up, FovDeg, this);
        }
    }

    public bool TryPose(double3 followedEcl, out double3 offsetFromFollowed, out double3 forwardEcl, out double3 upEcl, out double fovDeg)
    {
        fovDeg = FovDeg;
        offsetFromFollowed = default;
        if (!TryLook(out double3 eye, out forwardEcl, out upEcl)) return false;

        offsetFromFollowed = eye - followedEcl;
        return Vec.IsFinite(offsetFromFollowed);
    }

    // Straight down over the view's centre, with the screen's up along the course axis the view is turned to.
    private bool TryLook(out double3 eyeEcl, out double3 forwardEcl, out double3 upEcl)
    {
        eyeEcl = forwardEcl = upEcl = default;
        if (_body is not { } body) return false;

        eyeEcl = LocalToEcl(body, new double3(_view.Centre.X, _view.Centre.Y, _view.HeightM));
        forwardEcl = -DirectionToEcl(body, new double3(0, 0, 1));
        upEcl = DirectionToEcl(body, new double3(_view.ScreenUp.X, _view.ScreenUp.Y, 0));
        return Vec.IsFinite(eyeEcl) && Vec.IsFinite(upEcl);
    }

    // ---- input, from the GUI pass -----------------------------------------------------------------

    /// <summary>The keys and the mouse, read through ImGui after the panel has had its turn.</summary>
    public void Input(double dtPlayer)
    {
        if (ImGui.IsKeyPressed(ImGuiKey.B, repeat: false) && !ImGui.GetIO().WantTextInput)
        {
            Toggle();
            return;
        }

        if (!Active || _body is null) return;

        ImGuiIOPtr io = ImGui.GetIO();
        bool keys = !io.WantTextInput;
        bool mouse = !io.WantCaptureMouse;

        if (keys)
        {
            double pan = 0.6 * dtPlayer;
            double right = (Down(ImGuiKey.D) || Down(ImGuiKey.RightArrow) ? pan : 0.0) - (Down(ImGuiKey.A) || Down(ImGuiKey.LeftArrow) ? pan : 0.0);
            double up = (Down(ImGuiKey.W) || Down(ImGuiKey.UpArrow) ? pan : 0.0) - (Down(ImGuiKey.S) || Down(ImGuiKey.DownArrow) ? pan : 0.0);
            if (right != 0.0 || up != 0.0) _view.Pan(right, up);

            if (Pressed(ImGuiKey.Q)) _view.Turn(1);
            if (Pressed(ImGuiKey.E)) _view.Turn(-1);
            if (Pressed(ImGuiKey.R)) Rotate();
            if (Pressed(ImGuiKey.M)) Mirror();
            if (Pressed(ImGuiKey.PageUp)) _level++;
            if (Pressed(ImGuiKey.PageDown)) _level = Math.Max(0, _level - 1);
            if (Pressed(ImGuiKey.Delete) || Pressed(ImGuiKey.X)) RemoveUnderCursor();
            if (io.KeyCtrl && Pressed(ImGuiKey.Z)) Undo();
            if (io.KeyCtrl && Pressed(ImGuiKey.Y)) Redo();
            for (int n = 0; n < Math.Min(9, PieceCatalogue.All.Length); n++)
            {
                if (Pressed(ImGuiKey._1 + n)) Select(n);
            }

            if (Pressed(ImGuiKey._0)) Select(-1);
        }

        if (mouse)
        {
            if (io.MouseWheel != 0f) _view.Zoom(io.MouseWheel);

            // Either button dragged pans, a screen's worth of drag moving the ground a screen's worth.
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right) || ImGui.IsMouseDown(ImGuiMouseButton.Middle))
            {
                float2 d = io.MouseDelta;
                double perPixel = 2.0 * Math.Tan(double.DegreesToRadians(FovDeg) / 2.0) / Math.Max(1f, io.DisplaySize.Y);
                _view.Pan(-d.X * perPixel, d.Y * perPixel);
            }

            if (io.MouseDelta.X != 0f || io.MouseDelta.Y != 0f) _aimed = null;
            AimFromCursor();

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left, repeat: false)) Click();
        }

        Refresh();
    }

    private static bool Down(ImGuiKey key) => ImGui.IsKeyDown(key);

    private static bool Pressed(ImGuiKey key) => ImGui.IsKeyPressed(key, repeat: false);

    private void AimFromCursor()
    {
        if (_aimed is { } aimed)
        {
            _cursor = aimed;
            return;
        }

        if (_body is not { } body || !KsaWorld.TryCursorRayEcl(out double3 origin, out double3 direction)) return;

        double3 o = EclToLocal(body, origin), d = DirectionToLocal(body, direction);
        if (BuildView.OnPlane(o, d, _level * CourseSize.LevelM) is { } hit) _cursor = BuildView.CellAt(hit);
    }

    // ---- editing ----------------------------------------------------------------------------------

    public void Select(int index)
    {
        Selected = index >= 0 && index < PieceCatalogue.All.Length ? index : -1;
        if (Selected >= 0 && Array.IndexOf(PieceCatalogue.All[Selected].DistinctOrientations(), _orientation) < 0) _orientation = Orientation.Identity;
    }

    public void Rotate()
    {
        if (Selected >= 0) _orientation = Ghost.Next(PieceCatalogue.All[Selected], _orientation);
    }

    public void Mirror()
    {
        if (Selected >= 0) _orientation = Ghost.Mirrored(PieceCatalogue.All[Selected], _orientation);
    }

    public void Undo()
    {
        if (_edits.Undo()) Changed();
    }

    public void Redo()
    {
        if (_edits.Redo()) Changed();
    }

    public void Clear()
    {
        _edits.Apply(Course.Empty);
        Changed();
    }

    public void LoadSample()
    {
        _edits.Apply(SampleCourse.Build());
        Changed();
    }

    /// <summary>Points the cursor at a cell until the mouse next moves, for the bridge.</summary>
    public void AimAt(Cell cell)
    {
        _aimed = cell;
        _cursor = cell;
        Refresh();
    }

    /// <summary>A click: lay the ghost down, or with nothing chosen, pick up the piece under the cursor to move it.</summary>
    public void Click()
    {
        Refresh();
        if (Selected < 0)
        {
            PickUp();
            return;
        }

        if (_ghost is not { Placeable: true } ghost) return;

        _edits.Apply(Course.With(ghost.Piece));
        _level = ghost.Piece.Level + Math.Max(0, ghost.Piece.Def.Ports.Max(p => p.Level));
        Changed();
    }

    private void PickUp()
    {
        if (_cursor is not { } cell) return;

        int at = Course.PieceAt(cell);
        if (at < 0) return;

        Placed piece = Course.Pieces[at];
        _edits.Apply(Course.Without(at));
        Selected = Array.IndexOf(PieceCatalogue.All, piece.Def);
        _orientation = piece.Orientation;
        _level = piece.Level;
        Changed();
    }

    public void RemoveUnderCursor()
    {
        if (_cursor is not { } cell) return;

        int at = Course.PieceAt(cell);
        if (at < 0) return;

        _edits.Apply(Course.Without(at));
        Changed();
    }

    private void Changed()
    {
        _courseLines = CourseLines.Of(Course);
        Report = Course.Check();
        Relevel();
        Refresh();
        Publish();
    }

    // Hands the laid course to the ball and to the physics a kitten stands on, and takes it back when there is none.
    private void Publish()
    {
        if (_body is not { } body || Course.Pieces.Count == 0)
        {
            golf.Course = null;
            CourseCollider.Want(null, []);
            return;
        }

        golf.Course = (body, new CourseSurface(Course, LocalToCcf(Vec.Zero), _iCcf, _jCcf, _upCcf, new Golf.BodyGround(body), GolfBall.RadiusM));
        CourseCollider.Want(body, CourseColliders.Of(Course).Select(b => CourseCollider.Place(LocalToCcf(b.Centre), LocalDirectionToCcf(b.X),
                                                                                                LocalDirectionToCcf(b.Y), LocalDirectionToCcf(b.Z), b.Size)));
    }

    private double3 LocalDirectionToCcf(double3 local) => (_iCcf * local.X) + (_jCcf * local.Y) + (_upCcf * local.Z);

    // The floor just above the highest ground under any corner, so no piece is buried.
    private void Relevel()
    {
        if (_body is not { } body) return;

        var ground = new Golf.BodyGround(body);
        double high = _groundAtAnchor;
        foreach (Placed piece in Course.Pieces)
        {
            (int w, int h) = piece.Footprint;
            for (int a = 0; a <= w; a++)
            {
                for (int b = 0; b <= h; b++)
                {
                    double3 corner = LocalToCcf(new double3((piece.Origin.I + a) * CourseSize.CellM, (piece.Origin.J + b) * CourseSize.CellM, 0));
                    if (ground.TrySurfaceRadius(Vec.Unit(corner), out double r)) high = Math.Max(high, r);
                }
            }
        }

        _floorRadius = high + FloorAboveGroundM;
    }

    private void Refresh()
    {
        _ghost = Selected >= 0 && _cursor is { } cell ? Ghost.At(Course, PieceCatalogue.All[Selected], _orientation, _level, cell) : null;

        if (Course.Pieces.Count == 0) Status = "empty: start with a tee (1)";
        else if (Report.Playable) Status = $"tee reaches the cup through {Course.Pieces.Count} pieces";
        else Status = string.Join("; ", Report.Problems.Take(3).Select(p => p.Piece >= 0 ? $"piece {p.Piece + 1}: {p.What}" : p.What));
    }

    // ---- saving -----------------------------------------------------------------------------------

    public static string Folder => Path.Combine(Directory.GetParent(Log.Folder)?.FullName ?? Log.Folder, "KSAGolf", "Courses");

    public static IEnumerable<string> Saved()
        => Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order() : [];

    public bool Save()
    {
        if (_body is not { } body) return false;

        if (Name.Length == 0)
        {
            int n = 1;
            while (Saved().Contains($"Course {n}")) n++;
            Name = $"Course {n}";
        }

        double3 north = Vec.Unit(Vec.RejectFrom(new double3(0, 0, 1), _upCcf));
        double3 east = Vec.Cross(north, _upCcf);
        var where = new CourseFile
        {
            Name = Name,
            Body = body.Id,
            LatitudeDeg = double.RadiansToDegrees(Math.Asin(Math.Clamp(_upCcf.Z, -1.0, 1.0))),
            LongitudeDeg = double.RadiansToDegrees(Math.Atan2(_upCcf.Y, _upCcf.X)),
            HeadingDeg = double.RadiansToDegrees(Math.Atan2(Vec.Dot(_jCcf, east), Vec.Dot(_jCcf, north))),
            FloorAboveGroundM = _floorRadius - _groundAtAnchor,
        };

        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, $"{Name}.json"), CourseFile.Of(Course, where).ToJson());
            Status = $"saved as {Name}";
            Log.Info($"course '{Name}' saved: {Course.Pieces.Count} pieces on {body.Id}");
            return true;
        }
        catch (Exception e)
        {
            Status = $"could not save: {e.Message}";
            Log.Warn(Status);
            return false;
        }
    }

    public bool Load(string name)
    {
        CourseFile? file;
        try
        {
            file = CourseFile.FromJson(File.ReadAllText(Path.Combine(Folder, $"{name}.json")), out string why);
            if (file is null)
            {
                Status = $"{name}: {why}";
                return false;
            }
        }
        catch (Exception e)
        {
            Status = $"could not read {name}: {e.Message}";
            return false;
        }

        if (KsaWorld.FindCelestial(file.Body) is not { } body)
        {
            Status = $"{name} is on {file.Body}, which this world does not have";
            return false;
        }

        double lat = double.DegreesToRadians(file.LatitudeDeg), lon = double.DegreesToRadians(file.LongitudeDeg);
        double3 up = new(Math.Cos(lat) * Math.Cos(lon), Math.Cos(lat) * Math.Sin(lon), Math.Sin(lat));
        double3 north = Vec.Unit(Vec.RejectFrom(new double3(0, 0, 1), up));
        double3 east = Vec.Cross(north, up);
        double heading = double.DegreesToRadians(file.HeadingDeg);
        double3 j = (north * Math.Cos(heading)) + (east * Math.Sin(heading));

        Frame(body, up, Vec.Cross(j, up), j);
        _edits = new CourseEdits(file.ToCourse(out List<string> unknown));
        Name = file.Name.Length > 0 ? file.Name : name;
        Changed();

        double drift = (_floorRadius - _groundAtAnchor) - file.FloorAboveGroundM;
        if (Math.Abs(drift) > 0.05) Log.Warn($"course '{Name}' stands {drift * 100:F0} cm off where it was saved: the ground under it has changed");
        if (unknown.Count > 0) Status = $"left out pieces this version does not know: {string.Join(", ", unknown)}";
        Log.Info($"course '{Name}' loaded: {Course.Pieces.Count} pieces on {body.Id}");
        return true;
    }

    // ---- drawing ----------------------------------------------------------------------------------

    /// <summary>
    /// The pieces that have meshes, into every viewport; and the course, the ghost, the cursor and the grid as
    /// gizmo lines, into the main viewport only, which is the one the gizmo renderer draws.
    /// </summary>
    public void Draw(IViewport viewport)
    {
        if (_body is not { } body) return;
        if (!Active && Course.Pieces.Count == 0) return;

        double3 camera = viewport.GetCamera().PositionEcl;
        double3 origin = LocalToEcl(body, Vec.Zero) - camera;
        if (!Vec.IsFinite(origin) || Vec.Len(origin) > 2000.0) return;

        var axes = (I: DirectionToEcl(body, new double3(1, 0, 0)), J: DirectionToEcl(body, new double3(0, 1, 0)), Up: DirectionToEcl(body, new double3(0, 0, 1)));
        DrawMeshes(viewport, origin, axes.I, axes.J, axes.Up);
        if (!ReferenceEquals(viewport, Program.MainViewport)) return;

        GizmosRenderer gizmos = Program.GizmosRenderer;
        void Line(double3 a, double3 b, float4 colour)
            => gizmos.DrawLine(origin + (axes.I * a.X) + (axes.J * a.Y) + (axes.Up * a.Z), origin + (axes.I * b.X) + (axes.J * b.Y) + (axes.Up * b.Z), colour);

        foreach (CourseLine line in _courseLines)
        {
            bool meshed = line.Piece >= 0 && line.Piece < Course.Pieces.Count && HasModel(Course.Pieces[line.Piece]);
            if (!meshed || line.Kind == LineKind.Faulty || (Active && line.Kind == LineKind.Opening)) Line(line.From, line.To, ColourOf(line.Kind));
        }

        if (!Active) return;

        double c = CourseSize.CellM, z = -0.005;
        int ci = (int)Math.Floor(_view.Centre.X / c), cj = (int)Math.Floor(_view.Centre.Y / c);
        for (int k = -GridReach; k <= GridReach + 1; k++)
        {
            Line(new((ci + k) * c, (cj - GridReach) * c, z), new((ci + k) * c, (cj + GridReach + 1) * c, z), Dim);
            Line(new((ci - GridReach) * c, (cj + k) * c, z), new((ci + GridReach + 1) * c, (cj + k) * c, z), Dim);
        }

        if (_ghost is { } ghost)
        {
            float4 colour = ghost.Fit switch
            {
                Fit.Connects => Green,
                Fit.Open => Amber,
                _ => Red,
            };
            foreach (CourseLine line in CourseLines.Of(new Course([ghost.Piece]))) Line(line.From + new double3(0, 0, 0.01), line.To + new double3(0, 0, 0.01), colour);
        }

        if (_cursor is { } cell)
        {
            double h = (_level * CourseSize.LevelM) + 0.015, x0 = cell.I * c, y0 = cell.J * c, inset = 0.06;
            Line(new(x0 + inset, y0 + inset, h), new(x0 + c - inset, y0 + inset, h), Cyan);
            Line(new(x0 + c - inset, y0 + inset, h), new(x0 + c - inset, y0 + c - inset, h), Cyan);
            Line(new(x0 + c - inset, y0 + c - inset, h), new(x0 + inset, y0 + c - inset, h), Cyan);
            Line(new(x0 + inset, y0 + c - inset, h), new(x0 + inset, y0 + inset, h), Cyan);
        }
    }

    private const string Plinth = "KSAGolf_Piece_Plinth_Glb";

    private void DrawMeshes(IViewport viewport, double3 originEgo, double3 i, double3 j, double3 up)
    {
        SuperMeshRenderSystem system = Program.Instance.SuperMeshRenderSystem;
        double c = CourseSize.CellM;
        foreach (Placed piece in Course.Pieces)
        {
            if (Model(piece) is not { } model || MeshOf(model.Id, piece.Def.Id) is not { } mesh) continue;

            (double2 x, double2 y, double2 at) = piece.Placement(model.Turn);
            double lift = piece.Level * CourseSize.LevelM;
            Draw(mesh, (i * x.X) + (j * x.Y), (i * y.X) + (j * y.Y), up, originEgo + (i * at.X) + (j * at.Y) + (up * lift));

            // A piece laid above the ground stands on a block reaching down to it.
            if (piece.Level > 0 && MeshOf(Plinth, "plinth") is { } plinth)
            {
                (int w, int h) = piece.Footprint;
                Draw(plinth, i * (w * c), j * (h * c), up * lift,
                     originEgo + (i * (piece.Origin.I * c)) + (j * (piece.Origin.J * c)) + (up * (lift - PieceShape.BaseDepthM)));
            }
        }

        void Draw(StaticMeshRenderable mesh, double3 ex, double3 ey, double3 ez, double3 t)
        {
            mesh.Transform = new float4x4((float)ex.X, (float)ex.Y, (float)ex.Z, 0f,
                                          (float)ey.X, (float)ey.Y, (float)ey.Z, 0f,
                                          (float)ez.X, (float)ez.Y, (float)ez.Z, 0f,
                                          (float)t.X, (float)t.Y, (float)t.Z, 1f);
            mesh.Draw(system.ViewForViewport(viewport));
        }
    }

    // The model a piece is drawn with and the turn it is drawn at: its own, turned; or, laid mirrored where
    // no turn matches, its mirror twin, which is already mirrored and only needs the turn.
    private static (string Id, Orientation Turn)? Model(Placed piece)
    {
        if (piece.Def.Mesh is not { } mesh) return null;
        if (piece.Def.AsTurn(piece.Orientation) is { } turn) return (mesh, turn);

        return piece.Def.MirrorMesh is { } mirror ? (mirror, piece.Orientation with { Mirrored = false }) : null;
    }

    private bool HasModel(Placed piece) => Model(piece) is { } model && MeshOf(model.Id, piece.Def.Id) is not null;

    private StaticMeshRenderable? MeshOf(string id, string forPiece)
    {
        if (_meshes.TryGetValue(id, out StaticMeshRenderable? mesh)) return mesh;

        if (!AttachmentMesh.TryBuild(id, Material, out mesh, out string why)) Log.Warn($"course piece '{forPiece}' is drawn as lines: {why}");
        _meshes[id] = mesh;
        return mesh;
    }

    /// <summary>Lets the meshes go, when the mod unloads.</summary>
    public void Release()
    {
        Leave();
        foreach (StaticMeshRenderable? mesh in _meshes.Values) mesh?.Dispose();
        _meshes.Clear();
    }

    private static float4 ColourOf(LineKind kind) => kind switch
    {
        LineKind.Outline => White,
        LineKind.Faulty => Red,
        LineKind.Cell => Grey,
        LineKind.Opening => Green,
        LineKind.Lane => Yellow,
        _ => Cyan,
    };

    /// <summary>What the bridge reports.</summary>
    public Dictionary<string, object?> ReportState() => new()
    {
        ["active"] = Active,
        ["name"] = Name,
        ["pieces"] = Course.Pieces.Count,
        ["playable"] = Report.Playable,
        ["status"] = Status,
        ["selected"] = Selected >= 0 ? PieceCatalogue.All[Selected].Id : null,
        ["orientation"] = $"{_orientation.Quarter}{(_orientation.Mirrored ? " mirrored" : "")}",
        ["level"] = _level,
        ["cursor"] = _cursor is { } cell ? new[] { cell.I, cell.J } : null,
        ["ghost"] = _ghost is { } ghost ? $"{ghost.Fit}{(ghost.Snapped ? ", snapped" : "")} at {ghost.Piece.Origin.I},{ghost.Piece.Origin.J}" : null,
        ["view"] = $"centre {_view.Centre.X:F1},{_view.Centre.Y:F1} height {_view.HeightM:F1} quarter {_view.Quarter}",
        ["floor_above_ground_cm"] = Math.Round((_floorRadius - _groundAtAnchor) * 100.0, 1),
    };

    // ---- the course's frame -----------------------------------------------------------------------

    // Course-local metres (X along I, Y along J, Z up from the floor) and the body-fixed frame.
    private double3 LocalToCcf(double3 local) => _anchorCcf + (_upCcf * (_floorRadius - _groundAtAnchor)) + (_iCcf * local.X) + (_jCcf * local.Y) + (_upCcf * local.Z);

    private double3 LocalToEcl(Celestial body, double3 local) => body.GetPositionEcl() + double3.Transform(LocalToCcf(local), body.GetCcf2Cce());

    private double3 DirectionToEcl(Celestial body, double3 local)
        => double3.Transform((_iCcf * local.X) + (_jCcf * local.Y) + (_upCcf * local.Z), body.GetCcf2Cce());

    private double3 EclToLocal(Celestial body, double3 ecl)
    {
        double3 d = ToCcf(body, ecl) - LocalToCcf(Vec.Zero);
        return new double3(Vec.Dot(d, _iCcf), Vec.Dot(d, _jCcf), Vec.Dot(d, _upCcf));
    }

    private double3 DirectionToLocal(Celestial body, double3 ecl)
    {
        double3 d = double3.Transform(ecl, body.GetCce2Ccf());
        return new double3(Vec.Dot(d, _iCcf), Vec.Dot(d, _jCcf), Vec.Dot(d, _upCcf));
    }

    private static double3 ToCcf(Celestial body, double3 ecl) => double3.Transform(ecl - body.GetPositionEcl(), body.GetCce2Ccf());
}
