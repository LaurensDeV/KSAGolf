using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Commands from outside the game, read from a folder beside the log and answered in another:
/// what lets an agent in a terminal pause, move the camera, pose a kitten, photograph it and
/// load a save in a game that stays running. <c>tools/ksa-mcp/server.py</c> is the other
/// end, and <c>docs/VISUAL-TESTING.md</c> is why.
///
/// <para><b>Files, not a socket.</b> The mod does not reach the network, and a folder needs no port
/// and works across the WSL boundary as it is.</para>
///
/// <para>One command at a time, oldest first. A command that takes frames — a step, a capture —
/// holds the queue until it answers, so a sequence of them is a script that runs in order.</para>
/// </summary>
internal sealed class Bridge
{
    private readonly Config _config;

    // A few times a second is quick enough for a person and costs one directory listing.
    private const double PollSeconds = 0.1;
    private double _sincePoll;

    private Func<double, double, Reply?>? _running;
    private BridgeCommand? _current;

    private KsaWorld.MainView _saved;
    private Vehicle? _posedFrom;
    private SceneCamera? _scene;

    private readonly Golf _golf;
    private readonly CourseBuilder _builder;

    public Bridge(Config config, Golf golf, CourseBuilder builder)
    {
        _config = config;
        _golf = golf;
        _builder = builder;
    }

    /// <summary>Hands a held view back, so unloading does not leave the player in a pose nothing drives.</summary>
    public void Release()
    {
        if (_scene is null) return;

        _scene = null;
        KsaWorld.TryHandBackMainView(_saved, _posedFrom, out _, out _);
        _posedFrom = null;
    }

    // Asked for from the panel, and taken up by the next Update: the panel draws in the UI pass and
    // the bridge runs in the frame hook, and a capture must not start inside the UI pass.
    private static bool _playerAsked;

    /// <summary>Asks for a capture of what the player is looking at, with a note of the state.</summary>
    public static void RequestPlayerCapture() => _playerAsked = true;

    /// <summary>The folder the last one went to, for the panel to say so.</summary>
    public static string? LastPlayerCapture { get; private set; }

    private static string Root => Path.Combine(Log.Folder, "bridge");
    private static string Inbox => Path.Combine(Root, "in");
    private static string Outbox => Path.Combine(Root, "out");

    private readonly record struct Reply(bool Ok, string Error, Dictionary<string, object?> Data);

    private static Reply Done(Dictionary<string, object?>? data = null) => new(true, string.Empty, data ?? []);
    private static Reply Failed(string why) => new(false, why, []);

    /// <summary>
    /// One frame: advances the command running, or takes the next. Called from the one hook KSA
    /// always calls, so it runs in the menu and with the UI hidden. Nothing here may throw.
    /// </summary>
    public void Update(double dtPlayer, double dtSim)
    {
        try
        {
            if (_running is not null && _current is not null)
            {
                if (_running(dtPlayer, dtSim) is { } reply) Answer(_current, reply);
                return;
            }

            if (_playerAsked)
            {
                _playerAsked = false;
                StartPlayerCapture();
                return;
            }

            _sincePoll += dtPlayer;
            if (_sincePoll < PollSeconds) return;
            _sincePoll = 0.0;

            if (!Directory.Exists(Inbox)) return;

            string? next = Directory.GetFiles(Inbox, "*.json").Order(StringComparer.Ordinal).FirstOrDefault();
            if (next is null) return;

            string text = File.ReadAllText(next);
            File.Delete(next);

            if (!BridgeCommand.TryParse(text, out BridgeCommand? command, out string trouble))
            {
                Log.Warn($"bridge: refused {Path.GetFileName(next)} -- {trouble}");
                return;
            }

            Start(command!);
        }
        catch (Exception e)
        {
            if (_current is { } command) Answer(command, Failed($"threw: {e.Message}"));
            else Log.Warn($"bridge: {e.Message}");
        }
    }

    // Eight frames as quickly as KSA will take them, and a note written first, so it is the state at
    // the moment the player pressed rather than after the frames.
    private void StartPlayerCapture()
    {
        string id = "player-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string folder = Path.Combine(Outbox, id);
        Directory.CreateDirectory(folder);

        Dictionary<string, object?> note = new()
        {
            ["status"] = Status().Data,
            ["config"] = typeof(Config).GetFields(BindingFlags.Public | BindingFlags.Instance)
                                       .ToDictionary(f => f.Name, f => (object?)Convert.ToString(
                                                         f.GetValue(_config), CultureInfo.InvariantCulture)),
        };
        File.WriteAllText(Path.Combine(folder, "note.json"), JsonSerializer.Serialize(note, JsonOptions));

        try
        {
            // Shared, because the log is held open for writing the whole session.
            using FileStream stream = new(Path.Combine(Log.Folder, "KSAGolf.log"), FileMode.Open,
                                          FileAccess.Read, FileShare.ReadWrite);
            using StreamReader reader = new(stream);
            string[] log = reader.ReadToEnd().Split('\n');
            File.WriteAllLines(Path.Combine(folder, "log-tail.txt"), log.Skip(Math.Max(0, log.Length - 300)));
        }
        catch (IOException)
        {
            // A note without the log's tail still says what the state was.
        }

        LastPlayerCapture = folder;
        Log.Info($"capture for Claude: {folder}");

        string request = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = id,
            ["cmd"] = "capture",
            ["label"] = "player",
            ["frames"] = 8,
            ["every_frames"] = 1,
        });

        if (BridgeCommand.TryParse(request, out BridgeCommand? command, out _)) Start(command!);
    }

    /// <summary>Holds the view a <c>frame</c> command composed. From the camera pass.</summary>
    public void DriveCamera()
    {
        try
        {
            _scene?.Drive();
        }
        catch
        {
            // The view is the player's to take back; a pose that cannot be held is simply not held.
        }
    }

    private void Start(BridgeCommand command)
    {
        _current = command;
        Log.Info($"bridge: {command.Name} ({command.Id})");

        Reply? now = command.Name switch
        {
            "status" => Status(),
            "pause" => KsaWorld.SetPaused(true) ? Done() : Failed("the world would not pause"),
            "resume" => KsaWorld.SetPaused(false) ? Done() : Failed("the world would not resume"),
            "speed" => KsaWorld.SetSimulationSpeed(command.Number("x", 1.0)) ? Done() : Failed("speed refused"),
            "set" => Set(command),
            "get" => Get(command),
            "kitten_pose" => KittenPose(command),
            "kitten_prop" => KittenProp(command),
            "golf" => GolfCommand(command),
            "course" => CourseCommand(command),
            "player_capture" => PressTheButton(),
            "frame" => Frame(command),
            "orbit_place" => OrbitPlace(command),
            "step" => BeginStep(command),
            "capture" => BeginCapture(command),
            "load" => BeginLoad(command),
            _ => Failed($"no command '{command.Name}'"),
        };

        if (now is { } reply) Answer(command, reply);
    }

    private void Answer(BridgeCommand command, Reply reply)
    {
        _running = null;
        _current = null;

        Directory.CreateDirectory(Outbox);

        Dictionary<string, object?> body = new()
        {
            ["id"] = command.Id,
            ["cmd"] = command.Name,
            ["ok"] = reply.Ok,
            ["error"] = reply.Ok ? null : reply.Error,
            ["data"] = reply.Data,
        };

        // Written aside and moved, so the other end never reads half a reply.
        string final = Path.Combine(Outbox, command.Id + ".json");
        string partial = final + ".part";
        File.WriteAllText(partial, JsonSerializer.Serialize(body, JsonOptions));
        File.Move(partial, final, overwrite: true);

        if (!reply.Ok) Log.Warn($"bridge: {command.Name} ({command.Id}) failed -- {reply.Error}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    // ---- commands that answer at once -------------------------------------------------------

    private Reply Status() => Done(new()
    {
        ["build"] = Build.Version,
        ["in_flight"] = KsaWorld.InFlightScene,
        ["craft"] = KsaWorld.ControlledVehicle is { } craft ? KsaWorld.DisplayName(craft) : null,
        ["paused"] = KsaWorld.IsPaused,
        ["speed"] = KsaWorld.SimulationSpeed,
        ["camera_held"] = _scene is not null,
    });

    private Reply Set(BridgeCommand command)
    {
        string field = command.String("name");
        if (!command.TryRaw("value", out JsonElement value)) return Failed("set needs a name and a value");

        if (!BridgeCommand.TrySetField(_config, field, value, out string trouble)) return Failed(trouble);

        // The panel's tick box does this beside the write; the field alone changes nothing.
        if (field == nameof(Config.VerboseLog)) Log.Threshold = _config.VerboseLog ? Log.Level.Debug : Log.Level.Info;

        return Done(new() { [field] = BridgeCommand.FieldText(_config, field) });
    }

    private Reply Get(BridgeCommand command)
    {
        string field = command.String("name");
        return BridgeCommand.FieldText(_config, field) is { } text
                   ? Done(new() { [field] = text })
                   : Failed($"no field '{field}' on Config");
    }

    // The course builder without a hand on the mouse: "action" is one of enter, leave, sample, clear, place,
    // rotate, mirror, remove, undo, redo, save and load (with "name"); "piece" chooses a piece by id (or
    // "none" to pick pieces up); "i" and "j" aim the cursor at a cell. Answers with the builder's state.
    private Reply? CourseCommand(BridgeCommand command)
    {
        if (command.Has("piece")) _builder.Select(Array.FindIndex(PieceCatalogue.All, d => d.Id == command.String("piece")));
        if (command.Has("i") && command.Has("j")) _builder.AimAt(new Cell((int)command.Number("i", 0), (int)command.Number("j", 0)));

        switch (command.String("action"))
        {
            case "enter": _builder.Enter(); break;
            case "leave": _builder.Leave(); break;
            case "sample": _builder.LoadSample(); break;
            case "clear": _builder.Clear(); break;
            case "place": _builder.Click(); break;
            case "rotate": _builder.Rotate(); break;
            case "mirror": _builder.Mirror(); break;
            case "remove": _builder.RemoveUnderCursor(); break;
            case "undo": _builder.Undo(); break;
            case "redo": _builder.Redo(); break;
            case "save": _builder.Save(); break;
            case "load": _builder.Load(command.String("name")); break;
        }

        return Done(_builder.ReportState());
    }

    // Putting without a hand on the mouse: "action" is drop, pickup or address (which also steps away),
    // and "swing" is the mouse's horizontal movement in pixels, one entry a frame, fed as the player's
    // would be. Answers with the state once the swing has been fed and "wait_s" more have passed.
    private Reply? GolfCommand(BridgeCommand command)
    {
        switch (command.String("action"))
        {
            case "drop": _golf.DropBall(); break;
            case "pickup": _golf.PickUpBall(); break;
            case "address": _golf.AddressOrStepAway(); break;
        }

        if (command.Has("look_yaw_deg")) _golf.LookBy(command.Number("look_yaw_deg", 0.0));

        List<double> swing = [];
        if (command.TryRaw("swing", out JsonElement moves) && moves.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement move in moves.EnumerateArray())
            {
                if (move.TryGetDouble(out double dx)) swing.Add(dx);
            }
        }

        double wait = command.Number("wait_s", 0.0);
        if (swing.Count == 0 && wait <= 0.0) return Done(_golf.Report());

        int next = 0;
        double waited = 0.0;
        _running = (dtPlayer, _) =>
        {
            if (next < swing.Count)
            {
                _golf.Nudge(swing[next++]);
                return null;
            }

            waited += dtPlayer;
            return waited >= wait ? Done(_golf.Report()) : null;
        };

        return null;
    }

    private static Reply KittenProp(BridgeCommand command)
    {
        if (!command.Flag("on", true)) return Done(KittenPropProbe.Remove());

        string bone = command.String("bone");
        string kind = command.String("mesh");
        return Done(KittenPropProbe.Apply(kind.Length == 0 ? "helmet" : kind, bone.Length == 0 ? "Wrist_R" : bone, (float)command.Number("scale", 0.3),
                                          new float3((float)command.Number("rx", 0), (float)command.Number("ry", 0), (float)command.Number("rz", 0)),
                                          new float3((float)command.Number("x", 0), (float)command.Number("y", 0), (float)command.Number("z", 0)),
                                          command.TryRaw("matrix", out JsonElement m) && m.ValueKind == JsonValueKind.Array
                                              ? m.EnumerateArray().Select(e => e.GetSingle()).ToArray() : null));
    }

    private static Reply KittenPose(BridgeCommand command)
    {
        if (!command.Flag("on", true)) return Done(KittenPoseProbe.Remove());
        if (command.Flag("report", false)) return Done(KittenPoseProbe.Report());

        // Either "turns": [{"bone", "x", "y", "z", "deg", "swing", "hz", "phase", "move", "stretch", "shape", "from", "to"}]
        // with an optional "loop" in seconds, or one turn inline.
        List<KittenPoseProbe.Turn> turns = [];
        if (command.TryRaw("turns", out JsonElement list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement t in list.EnumerateArray())
            {
                float Axis(string n, double fallback) =>
                    t.TryGetProperty(n, out JsonElement v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : (float)fallback;
                string bone = t.TryGetProperty("bone", out JsonElement b) ? b.GetString() ?? "" : "";
                string shape = t.TryGetProperty("shape", out JsonElement sh) ? sh.GetString() ?? "" : "";
                bool move = t.TryGetProperty("move", out JsonElement mv) && mv.ValueKind == JsonValueKind.True;
                turns.Add(new(bone, new float3(Axis("x", 0), Axis("y", 0), Axis("z", 0)), Axis("deg", 0),
                              Axis("swing", 0), Axis("hz", 0), Axis("phase", 0), move,
                              Enum.TryParse(shape, true, out KittenPoseProbe.Shape s) ? s : KittenPoseProbe.Shape.Swing,
                              Axis("from", 0), Axis("to", double.PositiveInfinity),
                              t.TryGetProperty("stretch", out JsonElement st) && st.ValueKind == JsonValueKind.True));
            }
        }
        else
        {
            string bone = command.String("bone");
            turns.Add(new(bone.Length == 0 ? "Elbow_R" : bone,
                          new float3((float)command.Number("x", 0.0), (float)command.Number("y", 0.0), (float)command.Number("z", 1.0)),
                          (float)command.Number("deg", 45.0)));
        }

        if (turns.Any(t => t.Axis.LengthSquared() < 1e-6f)) return Failed("every turn needs an axis");

        if (command.Has("orbit_az")
            && !KsaWorld.TryWriteMainOrbit(double.DegreesToRadians(command.Number("orbit_az", 0.0)),
                                           double.DegreesToRadians(command.Number("orbit_el", 10.0))))
        {
            return Failed("the orbit camera would not turn");
        }

        if (command.Has("orbit_zoom") && !KittenPoseProbe.Zoom(command.Number("orbit_zoom", 1.0)))
        {
            return Failed("the orbit camera would not zoom");
        }

        return Done(KittenPoseProbe.Apply(turns, command.Flag("fly", false),
                                          command.Has("mmu") ? command.Flag("mmu", true) : null,
                                          command.Has("helmet") ? command.Flag("helmet", true) : null,
                                          (float)command.Number("loop", 0.0), (float)command.Number("rate", 1.0),
                                          command.Flag("rest", false)));
    }

    private static Celestial? FindBody(string id)
    {
        if (Universe.CurrentSystem is not { } system) return null;
        for (int i = 0; i < system.Count; i++)
        {
            if (system.GetIndex(i) is Celestial c && c.Id == id) return c;
        }

        return null;
    }

    // A camera held for a composed shot: "east", "north" and "up" metres from the craft being flown --
    // or with "own", "east" to its right, "north" ahead (negative behind) and "up" over its head --
    // looking at the craft "at" ("at_up" metres above it) or the body "at_body", at "fov" degrees.
    // "release" hands the view back.
    private Reply Frame(BridgeCommand command)
    {
        if (command.Flag("release", false))
        {
            if (_scene is null) return Done();
            _scene = null;
            KsaWorld.TryHandBackMainView(_saved, _posedFrom, out _, out _);
            _posedFrom = null;
            return Done();
        }

        if (KsaWorld.ControlledVehicle is not { } craft) return Failed("no craft is being flown");
        string at = command.String("at");
        Vehicle? target = at.Length > 0 ? KsaWorld.Vehicles.FirstOrDefault(v => KsaWorld.IsAlive(v) && KsaWorld.DisplayName(v) == at) : null;
        if (at.Length > 0 && target is null) return Failed($"no craft called '{at}'");

        if (_scene is null)
        {
            _saved = KsaWorld.RememberMainView();
            _posedFrom = craft;
        }

        _scene = new SceneCamera
        {
            Anchor = craft,
            EastNorthUp = new double3(command.Number("east", 0.0), command.Number("north", -5.0), command.Number("up", 1.0)),
            AtCraft = target,
            AtCraftUp = command.Number("at_up", 0.0),
            AtBody = command.String("at_body") is { Length: > 0 } b ? FindBody(b) : null,
            FovDeg = command.Number("fov", 50.0),
            Body = command.Flag("own", false) && craft is KittenEva k && TryKittenFrame(k, out double3 face, out double3 head)
                ? (face, head)
                : null,
            TurnDeg = command.Number("turn", 0.0),
            TiltDeg = command.Number("tilt", 0.0),
        };
        return Done(new() { ["held"] = _scene.Drive() });
    }

    // A craft put in a circular orbit "radius_m" from the centre of "body" over its sunlit side, offset
    // "radial_m" out, "north_m" and "east_m" from that point and moving east with it -- or from "beside"
    // another craft's own state, so two placed this way float together. A kitten is turned to face the
    // body, or "face_at" another craft, with its head to the north.
    private static Reply OrbitPlace(BridgeCommand command)
    {
        string named = command.String("craft");
        Vehicle? craft = named.Length > 0
            ? KsaWorld.Vehicles.FirstOrDefault(v => KsaWorld.IsAlive(v) && KsaWorld.DisplayName(v) == named)
            : KsaWorld.ControlledVehicle;
        if (craft is null) return Failed("no such craft");
        if (FindBody(command.String("body") is { Length: > 0 } b ? b : "Earth") is not { } body) return Failed("no such body");
        if (!KsaWorld.TryStarPositionEcl(out double3 star)) return Failed("no star");

        try
        {
            double3 centre = KsaWorld.PositionEcl(body);
            double3 radial = Vec.Unit(star - centre);

            // Beside another craft: its own state now, so the two are placed in the same instant and
            // stay together rather than a frame's flight apart.
            Vehicle? beside = command.String("beside") is { Length: > 0 } other
                ? KsaWorld.Vehicles.FirstOrDefault(x => KsaWorld.IsAlive(x) && KsaWorld.DisplayName(x) == other)
                : null;
            double3? besideAt = null, besideV = null;
            if (beside is not null)
            {
                besideAt = KsaWorld.PositionEcl(beside) - centre;
                besideV = KsaWorld.VelocityEcl(beside) - body.GetVelocityEcl();
                radial = Vec.Unit(besideAt.Value);
            }
            double3 axis = new double3(0, 0, 1).Transform(body.GetCci2Cce());
            double3 north = Vec.Unit(axis - (radial * Vec.Dot(axis, radial)));
            double3 east = Vec.Cross(north, radial);

            // Round from under the star by "sun_deg" toward the east, so the light comes from the side.
            if (beside is null && command.Number("sun_deg", 0.0) is var sun and not 0.0)
            {
                double a = double.DegreesToRadians(sun);
                radial = Vec.Unit((radial * Math.Cos(a)) + (east * Math.Sin(a)));
                north = Vec.Unit(axis - (radial * Vec.Dot(axis, radial)));
                east = Vec.Cross(north, radial);
            }

            double r = command.Number("radius_m", 26_000_000.0);
            double3 offset = (radial * command.Number("radial_m", 0.0)) + (north * command.Number("north_m", 0.0))
                             + (east * command.Number("east_m", 0.0));
            double3 at = (besideAt ?? (radial * r)) + offset;
            double3 v = besideV ?? (east * Math.Sqrt(((IParentBody)body).Mu / r));

            doubleQuat toCci = body.GetCce2Cci();
            Orbit orbit = Orbit.CreateFromStateCci(body, Universe.GetElapsedTime(), at.Transform(toCci), v.Transform(toCci), craft.OrbitColor);

            // Facing the body, or "face_at" another craft, head to the north either way.
            double3 facing = -radial;
            if (command.String("face_at") is { Length: > 0 } look
                && KsaWorld.Vehicles.FirstOrDefault(x => KsaWorld.IsAlive(x) && KsaWorld.DisplayName(x) == look) is { } seen)
            {
                double3 d = (KsaWorld.PositionEcl(seen) - centre) - at;
                d -= north * Vec.Dot(d, north);
                if (Vec.Len(d) > 1e-6) facing = Vec.Unit(d);
            }

            // And "face_turn_deg" further round to the kitten's own left.
            if (command.Number("face_turn_deg", 0.0) is var turn and not 0.0)
            {
                double t = double.DegreesToRadians(turn);
                double3 left = Vec.Cross(north, facing);
                facing = Vec.Unit((facing * Math.Cos(t)) + (left * Math.Sin(t)));
            }

            doubleQuat? attitude = craft is KittenEva kitten && TryKittenFrame(kitten, out double3 face, out double3 head)
                ? doubleQuat.Concatenate(craft.Body2Cce, Turning(face, head, facing, north))
                : null;
            craft.Teleport(orbit, attitude, double3.Zero);
            return Done(new() { ["altitude_km"] = Math.Round((r - body.MeanRadius) / 1000.0, 1) });
        }
        catch (Exception e)
        {
            return Failed($"could not place {KsaWorld.DisplayName(craft)}: {e.GetBaseException().Message}");
        }
    }

    // Which way a kitten faces and which way its head is, in the ecliptic, off its drawn skeleton.
    private static bool TryKittenFrame(KittenEva kitten, out double3 face, out double3 head)
    {
        face = head = default;
        if (KittenPoseProbe.AvatarOf(kitten)?.Core.CharacterModel is not { } model) return false;
        List<string> names = model.Skeleton.BoneNames ?? [];
        float4x4[] world = model.Skeleton.WorldTransforms.ToArray();
        int root = names.IndexOf("Root_M"), top = names.IndexOf("Head_M"), nose = names.IndexOf("NoseJoint_M");
        if (root < 0 || top < 0 || nose < 0 || world.Length <= Math.Max(root, Math.Max(top, nose))) return false;
        float4x4 drawn = model.Transform;
        double3 At(int i) { float3 p = (world[i] * drawn).Translation; return new double3(p.X, p.Y, p.Z); }
        head = Vec.Unit(At(top) - At(root));
        double3 n = At(nose) - At(top);
        face = Vec.Unit(n - (head * Vec.Dot(n, head)));
        return Vec.IsFinite(face) && Vec.IsFinite(head);
    }

    // The rotation carrying one facing-and-up pair onto another.
    private static doubleQuat Turning(double3 fromFace, double3 fromUp, double3 toFace, double3 toUp)
    {
        doubleQuat first = Arc(fromFace, toFace);
        double3 upNow = fromUp.Transform(first);
        upNow = Vec.Unit(upNow - (toFace * Vec.Dot(upNow, toFace)));
        double3 want = Vec.Unit(toUp - (toFace * Vec.Dot(toUp, toFace)));
        double roll = Math.Atan2(Vec.Dot(toFace, Vec.Cross(upNow, want)), Vec.Dot(upNow, want));
        return doubleQuat.Concatenate(first, doubleQuat.CreateFromAxisAngle(toFace, roll));
    }

    private static doubleQuat Arc(double3 a, double3 b)
    {
        a = Vec.Unit(a); b = Vec.Unit(b);
        double3 axis = Vec.Cross(a, b);
        double s = Vec.Len(axis);
        if (s < 1e-9) return Vec.Dot(a, b) > 0 ? doubleQuat.Identity : doubleQuat.CreateFromAxisAngle(Vec.Unit(Vec.AnyPerpendicular(a)), Math.PI);
        return doubleQuat.CreateFromAxisAngle(axis / s, Math.Atan2(s, Vec.Dot(a, b)));
    }

    // What the panel's Capture for Claude button does, for a test that cannot click it.
    private static Reply PressTheButton()
    {
        RequestPlayerCapture();
        return Done();
    }

    // ---- commands that take frames ----------------------------------------------------------

    // Runs the world for so many simulated seconds and stops it again, so what is photographed next
    // is at an age that was asked for rather than one that happened.
    private Reply? BeginStep(BridgeCommand command)
    {
        double seconds = command.Number("seconds", 1.0);
        if (!(seconds > 0.0)) return Failed("seconds must be positive");

        double advanced = 0.0;
        double waited = 0.0;
        KsaWorld.SetPaused(false);

        _running = (dtPlayer, dtSim) =>
        {
            waited += dtPlayer;
            advanced += Math.Max(dtSim, 0.0);

            if (advanced >= seconds)
            {
                KsaWorld.SetPaused(true);
                return Done(new() { ["advanced_s"] = Math.Round(advanced, 3) });
            }

            return waited > (seconds * 20.0) + 30.0 ? Failed($"only {advanced:F2} s passed") : null;
        };

        return null;
    }

    private Reply? BeginLoad(BridgeCommand command)
    {
        string save = command.String("save");
        if (save.Length == 0) return Failed("load needs a save");

        try
        {
            GameSaves.LoadSaveGame(save);
        }
        catch (Exception e)
        {
            return Failed($"could not load '{save}': {e.Message}");
        }

        _scene = null;
        double waited = 0.0;

        _running = (dtPlayer, _) =>
        {
            waited += dtPlayer;

            // A beat past the craft appearing, for the world to settle round it.
            if (waited > 3.0 && KsaWorld.InFlight)
            {
                return Done(new() { ["craft"] = KsaWorld.DisplayName(KsaWorld.ControlledVehicle!) });
            }

            return waited > 60.0 ? Failed("no craft after 60 s") : null;
        };

        return null;
    }

    // Photographs through KSA's own capture, which names files to the second: so each one is moved
    // aside and renamed before the next is asked for, and two can never be one file. Every picture
    // gets a manifest beside it -- the time, the camera and where the craft is on screen -- so nothing
    // has to be matched up by its time afterwards.
    private Reply? BeginCapture(BridgeCommand command)
    {
        string label = command.String("label");
        if (label.Length == 0) label = "shot";
        label = string.Concat(label.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

        int frames = Math.Clamp((int)command.Number("frames", 1.0), 1, 120);
        double everySim = Math.Max(command.Number("every_s", 0.0), 0.0);
        int everyFrames = Math.Max((int)command.Number("every_frames", 1.0), 1);

        string folder = Path.Combine(Outbox, command.Id);
        Directory.CreateDirectory(folder);

        string shots = Path.Combine(KSA.Constants.DocumentsFolderPath, "exports", "screenshots");

        List<Dictionary<string, object?>> taken = [];
        int index = 0;
        bool waiting = false;
        DateTime askedAt = default;
        Dictionary<string, object?>? manifest = null;
        double sinceSim = 0.0;
        int sinceFrames = 0;
        double waited = 0.0;
        int warming = 0;

        // Paused with a spacing asked for, the world is run that far and stopped again before each
        // picture, so the ages are exactly the ones asked for however long a command takes to arrive.
        bool stepping = KsaWorld.IsPaused && everySim > 0.0;
        int settle = 0;

        _running = (dtPlayer, dtSim) =>
        {
            if (!waiting)
            {
                sinceSim += Math.Max(dtSim, 0.0);
                sinceFrames++;

                if (stepping && index > 0)
                {
                    if (sinceSim < everySim)
                    {
                        if (KsaWorld.IsPaused) KsaWorld.SetPaused(false);
                        return null;
                    }

                    if (!KsaWorld.IsPaused)
                    {
                        KsaWorld.SetPaused(true);
                        settle = 0;
                    }

                    // A frame or two for the paused world to be the one drawn.
                    if (++settle < 3) return null;
                }

                bool due = index == 0 || stepping
                           || (everySim > 0.0 ? sinceSim >= everySim : sinceFrames >= everyFrames);
                if (!due) return null;

                askedAt = DateTime.UtcNow.AddSeconds(-0.5);

                // WITH THE UI HIDDEN FOR A MOMENT FIRST. The cloud's history is blended over frames,
                // and under the game's windows it holds no cloud -- so a screenshot, which hides the
                // UI for the one frame it takes, showed each window as a dark grainy rectangle on the
                // cloud. KSA's own warm-up hides it for these frames first; the manifest is written on
                // the frame the picture is actually taken, so it still describes that instant.
                if (!KsaWorld.TryRequestScreenshot(flags: $"warm={WarmFrames}"))
                {
                    return Failed("KSA would not take a screenshot");
                }

                manifest = null;
                warming = WarmFrames;
                waiting = true;
                waited = 0.0;
                return null;
            }

            if (manifest is null && --warming <= 0) manifest = Manifest(label, index);

            waited += dtPlayer;
            if (waited > 10.0) return Failed($"screenshot {index} never arrived");

            string? file = Directory.Exists(shots)
                               ? new DirectoryInfo(shots).GetFiles("ksa_*.png")
                                                         .Where(f => f.LastWriteTimeUtc >= askedAt && f.Length > 0)
                                                         .OrderByDescending(f => f.LastWriteTimeUtc)
                                                         .FirstOrDefault()?.FullName
                               : null;
            if (file is null) return null;

            string name = $"{index:D2}-{label}";
            string target = Path.Combine(folder, name + ".png");

            try
            {
                File.Move(file, target, overwrite: true);
            }
            catch (IOException)
            {
                // Still being written; the next frame will find it finished.
                return null;
            }

            manifest ??= Manifest(label, index);
            manifest["file"] = target;
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(manifest, JsonOptions));
            taken.Add(manifest);

            index++;
            waiting = false;
            sinceSim = 0.0;
            sinceFrames = 0;

            return index >= frames ? Done(new() { ["folder"] = folder, ["frames"] = taken }) : null;
        };

        return null;
    }

    // How many frames the UI is hidden for before a capture: the history keeps about an eighth of
    // each frame, so after these under a twentieth of what the windows left is still in it.
    private const int WarmFrames = 24;

    // What a picture was of, recorded on the frame it was taken.
    private static Dictionary<string, object?> Manifest(string label, int index)
    {
        Dictionary<string, object?> m = new()
        {
            ["label"] = label,
            ["index"] = index,
            ["wall"] = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
            ["paused"] = KsaWorld.IsPaused,
            ["speed"] = KsaWorld.SimulationSpeed,
            ["fov_deg"] = Math.Round(KsaWorld.MainViewFovDeg(), 2),
        };

        if (KsaWorld.ControlledVehicle is { } craft)
        {
            m["craft"] = KsaWorld.DisplayName(craft);
            m["craft_screen"] = Screen(KsaWorld.PositionEcl(craft));
        }

        return m;
    }

    // Where a point falls in the picture, as fractions from the top left, or null when it is behind
    // the camera. The same matrix the frame was drawn with, so a crop taken off it is exact.
    private static double[]? Screen(double3 pointEcl)
    {
        if (Program.GetMainCamera() is not { } camera) return null;

        double4 clip = camera.EgoToClipDouble(pointEcl - camera.PositionEcl);
        if (!(clip.W > 1e-6)) return null;

        return [Math.Round((clip.X / clip.W * 0.5) + 0.5, 4), Math.Round((clip.Y / clip.W * 0.5) + 0.5, 4)];
    }
}
