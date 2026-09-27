using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace KSAGolf;

/// <summary>
/// Putting: the flown kitten carries a putter, drops a ball, steps up to it, and swings the club with
/// the mouse, left to take it back and right to hit.
///
/// <para>The ball lives in the body-fixed frame of the world under it (see <see cref="GolfBall"/>) and is
/// drawn from <see cref="ViewDrawHook"/>. Stepping up to the ball is drawn, not walked: the kitten's
/// skeleton is carried into a <see cref="Stance"/> beside it while its physics body stays put, and
/// <see cref="GolfPose"/> bends it over the ball and swings the club from the angle held here. The view
/// is borrowed while addressing so the mouse has nothing else to do.</para>
/// </summary>
internal sealed class Golf(Config config) : IViewPose, IMouseSwing
{
    private enum Phase { Carrying, Addressing }

    /// <summary>How far the kitten is drawn stepping to the ball, in model centimetres.</summary>
    public const double StepReachCm = 90.0;

    // How long the step to the ball and back takes to draw.
    private const double StepSeconds = 0.35;

    // The mouse is not read for this long after stepping up to the ball: the hand pressing G is still
    // moving, and the club starts a centimetre from the ball.
    private const double SettleSeconds = 0.5;

    // How far the kitten's real body may drift while addressing before the stance is given up.
    private const double DriftM = 0.15;

    // How long the view may stay on a putt that has not stopped, before it is handed back anyway.
    private const double WatchSeconds = 6.0;

    private const double ViewFovDeg = 50.0;

    // Through the kitten's eyes: above and ahead of the middle of its head, which is hidden, so the
    // collar of its suit stays below the view.
    private static readonly double3 EyeFromHead = new(0, 5.0, 12.0);

    private const double FirstPersonFovDeg = 75.0;

    private readonly Config _config = config;
    private readonly PuttingStroke _stroke = new();

    private readonly PutterRig _rig = new();
    private Phase _phase;
    private Stance _stance;
    private double _blend;
    private double3 _standingCcf;

    private KittenEva? _kitten;
    private CharacterAvatar? _avatar;
    private StaticMeshRenderable? _club;
    private GolfPose? _pose;
    private bool _mmuWas = true;
    private bool _helmetWas = true;

    private GolfBall? _ball;
    private Celestial? _ballBody;
    private StaticMeshRenderable? _ballMesh;

    // The club as the kitten's own eyes see it at address, where the held one is folded away with the paws.
    private StaticMeshRenderable? _viewClub;

    private double3 _targetCcf;
    private double3 _viewOffsetCcf;
    private double3 _viewForwardCcf;
    private double3 _upCcf;
    private double _towards = 1.0;
    private double _sinceStrike = -1.0;
    private double _sinceAddress;
    private double3 _struckFromCcf;
    private KsaWorld.MainView _saved;
    private bool _borrowed;

    private double _dx;
    private double? _lastX;
    private double? _lastY;

    private readonly HeadLook _look = new();

    /// <summary>What the player is told under the controls.</summary>
    public string Status { get; private set; } = "";

    public bool Holding => _club is not null;
    public bool HasBall => _ball is not null;
    public bool Addressing => _phase == Phase.Addressing;
    public bool BallResting => _ball?.Resting ?? false;
    public double SwingDeg => double.RadiansToDegrees(_stroke.Theta);

    public bool UpIsExact => _config.FirstPerson;

    public IMouseDrag? Orbit => _borrowed && _phase == Phase.Carrying && _config.FirstPerson && !CursorWanted ? _look : null;

    // The mouse is taken to swing at address, and to look in first person -- never while the game is not
    // the window in front, and in first person not while Alt is held or a KSA window is open, which give
    // the cursor back so the panel can still be reached.
    public IMouseSwing? Swing => _borrowed && Focused && (Addressing || (_config.FirstPerson && !CursorWanted)) ? this : null;

    private static bool CursorWanted => ImGui.GetIO().KeyAlt || Program.IsWindowOpen;

    private static bool Focused
    {
        get
        {
            try
            {
                return Program.GetWindow().GetAttribute(GlfwWindowAttribute.Focused) != 0;
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>The ball's distance from the kitten, in metres, or null with no ball or no kitten.</summary>
    public double? BallDistance => _ball is not null && _kitten is not null && KsaWorld.IsAlive(_kitten)
        ? Vec.Len(BallEcl() - KsaWorld.PositionEcl(_kitten))
        : null;

    /// <summary>One simulation step, from whichever hook takes the frame.</summary>
    public void Step(double dtSim, double dtPlayer)
    {
        KittenEva? kitten = _config.PlayGolf && KsaWorld.InFlightScene ? KsaWorld.ControlledVehicle as KittenEva : null;
        if (!ReferenceEquals(kitten, _kitten))
        {
            LetGo();
            if (kitten is not null) Hold(kitten);
        }

        StepBall(dtSim);

        if (_kitten is null) return;
        if (_kitten.Parent is Celestial world) PadSurface.Want(world);

        double towards = _phase == Phase.Addressing ? 1.0 : 0.0;
        _blend = Math.Clamp(_blend + (Math.Sign(towards - _blend) * dtPlayer / StepSeconds), 0.0, 1.0);

        if (_phase == Phase.Addressing) Swinging(dtPlayer);
        DriveView();
    }

    // The view is borrowed while addressing, and while looking through the kitten's eyes.
    private void DriveView()
    {
        bool firstPerson = _config.FirstPerson && _kitten is not null && !_suspended;
        bool want = _kitten is not null && !_suspended && (_phase == Phase.Addressing || firstPerson);

        if (want && !_borrowed)
        {
            _saved = KsaWorld.RememberMainView();
            _borrowed = true;
            if (firstPerson) FaceTheLookForward();
        }
        else if (!want && _borrowed)
        {
            KsaWorld.TryHandBackMainView(_saved, _kitten, out _, out _);
            _borrowed = false;
        }

        FirstPersonHook.Body = firstPerson && _borrowed ? _pose : null;
        FirstPersonHook.Heading = firstPerson && _borrowed && _phase == Phase.Carrying ? LookHeading() : null;
        TurnToLook();

        if (_borrowed && _kitten is { } kitten && TryView(KsaWorld.PositionEcl(kitten), out double3 offset, out double3 forward, out double3 up))
        {
            KsaWorld.TryLookFromMainViewport(offset, forward, up, Fov, this);
        }
    }

    private double Fov => !_config.FirstPerson ? (_saved.Valid ? _saved.FovDeg : ViewFovDeg) : FirstPersonFovDeg;

    // The eye starts looking where the kitten faces, a little down.
    private void FaceTheLookForward()
    {
        if (_kitten is not { } kitten || !TryLocalFrame(kitten, out double3 up, out double3 north, out double3 east)) return;

        double3 facing = KittenFrame.DirectionToEcl(kitten, new double3(0, 0, 1));
        _look.LookTowards(Math.Atan2(Vec.Dot(facing, east), Vec.Dot(facing, north)), double.DegreesToRadians(-12.0));
    }

    private Posture Posture()
    {
        bool firstPerson = _config.FirstPerson && _borrowed;
        if (_phase == Phase.Carrying && firstPerson && _blend <= 0.0)
        {
            return new Posture(new Stance(_turnToLook, Vec.Zero), 1.0, null, true);
        }

        return new Posture(_stance, _blend, _phase == Phase.Addressing ? _stroke.Theta : null, firstPerson);
    }

    // How far the kitten is drawn turned to face the first-person look. A kitten standing still is asleep
    // on the terrain and KSA's servo does not turn it, so until it walks the turn is drawn; walking, the
    // body itself comes round to the look and this falls to nothing.
    private double _turnToLook;

    private void TurnToLook()
    {
        _turnToLook = 0.0;
        if (!_config.FirstPerson || !_borrowed || _phase != Phase.Carrying || _kitten is not { } kitten || LookHeading() is not { } heading) return;

        double3 inModel = KittenFrame.DirectionFromEcl(kitten, heading);
        if (Math.Abs(inModel.X) + Math.Abs(inModel.Z) > 1e-6) _turnToLook = Math.Atan2(inModel.X, inModel.Z);
    }

    // Where the first-person look faces along the ground: what the kitten turns to and walks by.
    private double3? LookHeading()
    {
        if (_kitten is not { } kitten || !TryLocalFrame(kitten, out _, out double3 north, out double3 east)) return null;

        return (north * Math.Cos(_look.Yaw)) + (east * Math.Sin(_look.Yaw));
    }

    // Up, north and east over the kitten, in the ecliptic.
    private static bool TryLocalFrame(KittenEva kitten, out double3 up, out double3 north, out double3 east)
    {
        up = north = east = default;
        if (kitten.Parent is not Celestial body) return false;

        up = Vec.Unit(KsaWorld.PositionEcl(kitten) - body.GetPositionEcl());
        double3 axis = double3.Transform(new double3(0, 0, 1), doubleQuat.Inverse(body.GetCce2Ccf()));
        north = Vec.Unit(Vec.RejectFrom(axis, up));
        east = Vec.Cross(north, up);
        return Vec.IsFinite(north) && Vec.Len2(north) > 0.5;
    }

    /// <summary>The keys, from the GUI pass: ImGui is what sees them.</summary>
    public void Keys()
    {
        if (!_config.PlayGolf || _suspended || ImGui.GetIO().WantTextInput) return;
        if (ImGui.IsKeyPressed(ImGuiKey.G, repeat: false)) AddressOrStepAway();
        if (ImGui.IsKeyPressed(ImGuiKey.V, repeat: false)) _config.FirstPerson = !_config.FirstPerson;
    }

    public void AddressOrStepAway()
    {
        if (_phase != Phase.Carrying)
        {
            Leave("");
            return;
        }

        TryAddress();
    }

    /// <summary>The course laid on a body, which the ball rolls on and banks off wherever it lies.</summary>
    public (Celestial Body, CourseSurface Surface)? Course { get; set; }

    private CourseSurface? CourseOn(Celestial body) => Course is { } laid && ReferenceEquals(laid.Body, body) ? laid.Surface : null;

    private IBodyGround GroundOn(Celestial body) => CourseOn(body) is { } course ? course : new BodyGround(body);

    /// <summary>The kitten carrying the putter, if one is.</summary>
    public KittenEva? Kitten => _kitten is { } kitten && KsaWorld.IsAlive(kitten) ? kitten : null;

    /// <summary>
    /// While something else holds the view -- the course builder -- golf neither borrows it nor reads its
    /// keys. Setting it hands golf's view back at once, so whoever takes over remembers the player's own.
    /// </summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            _suspended = value;
            if (!value) return;

            if (_phase != Phase.Carrying) Leave("");
            FirstPersonHook.Body = null;
            FirstPersonHook.Heading = null;
            if (!_borrowed) return;

            KsaWorld.TryHandBackMainView(_saved, _kitten, out _, out _);
            _borrowed = false;
        }
    }

    private bool _suspended;

    public void DropBall()
    {
        if (_kitten is not { } kitten || kitten.Parent is not Celestial body)
        {
            Status = "Fly a kitten on the ground to drop a ball.";
            return;
        }

        if (_phase != Phase.Carrying) Leave("");

        double3 spotCcf = ToCcf(body, KittenFrame.ToEcl(kitten, new PutterRig().Ball));
        double surface;
        if (!GroundOn(body).TrySurfaceRadius(Vec.Unit(spotCcf), out surface))
        {
            Status = "There is no ground here to drop a ball on.";
            return;
        }

        _ball = new GolfBall();
        _ball.Place(Vec.Unit(spotCcf) * (surface + GolfBall.RadiusM + 0.01));
        _ballBody = body;

        if (_ballMesh is null && !AttachmentMesh.TryBuild("KSAGolf_Ball_Glb", "KSAGolf_Ball_Material", out _ballMesh, out string why))
        {
            Log.Warn($"the ball cannot be drawn: {why}");
        }

        Status = "Ball down. Press G to step up to it.";
        Log.Info($"dropped a ball in front of {KsaWorld.DisplayName(kitten)}");
    }

    public void PickUpBall()
    {
        if (_phase != Phase.Carrying) Leave("");
        _ball = null;
        _ballBody = null;
        Status = "";
    }

    /// <summary>Everything that belonged to a world a save load has replaced.</summary>
    public void Forget()
    {
        LetGo();
        PadSurface.Forget();
        _ball = null;
        _ballBody = null;
        Status = "";
    }

    public void Release()
    {
        Forget();
        _ballMesh?.Dispose();
        _ballMesh = null;
        _viewClub?.Dispose();
        _viewClub = null;
    }

    /// <summary>Turns the first-person look without the mouse, for the bridge.</summary>
    public void LookBy(double yawDeg) => _look.LookTowards(_look.Yaw + double.DegreesToRadians(yawDeg), _look.Pitch);

    /// <summary>Mouse movement from somewhere other than the mouse: the bridge's scripted swing.</summary>
    public void Nudge(double dx) => _dx += dx;

    /// <summary>The state, for the bridge.</summary>
    public Dictionary<string, object?> Report()
    {
        Dictionary<string, object?> report = new()
        {
            ["phase"] = _phase.ToString(),
            ["holding"] = Holding,
            ["status"] = Status,
            ["theta_deg"] = Math.Round(SwingDeg, 1),
            ["struck"] = _stroke.Struck,
            ["ball_distance_m"] = BallDistance is { } d ? Math.Round(d, 3) : null,
        };

        if (_ball is { } ball)
        {
            report["ball_speed_mps"] = Math.Round(Vec.Len(ball.VelocityCcf), 3);
            report["ball_resting"] = ball.Resting;
            report["ball_on_ground"] = ball.OnGround;
            if (_kitten is { } kitten)
            {
                double3 model = KittenFrame.FromEcl(kitten, BallEcl());
                report["ball_in_kitten_cm"] = new[] { Math.Round(model.X, 1), Math.Round(model.Y, 1), Math.Round(model.Z, 1) };
            }
        }

        report["stance_yaw_deg"] = Math.Round(double.RadiansToDegrees(_stance.Yaw), 1);
        report["stance_offset_cm"] = new[] { Math.Round(_stance.Offset.X, 1), Math.Round(_stance.Offset.Y, 1), Math.Round(_stance.Offset.Z, 1) };
        report["blend"] = Math.Round(_blend, 2);
        report["last_sound"] = GolfSounds.LastPlayed;
        if (_kitten is { Parent: Celestial world } walker)
        {
            // The ground along the kitten's left, every six metres: terrain, and a launch site's top, against its feet.
            double3 feetEcl = KittenFrame.ToEcl(walker, Vec.Zero);
            double feetR = Vec.Len(ToCcf(world, feetEcl));
            List<string> profile = [];
            for (int m = 0; m <= 120; m += 6)
            {
                double3 at = Vec.Unit(ToCcf(world, KittenFrame.ToEcl(walker, new double3(m * 100.0, 0, 0))));
                double terrain = world.MeanRadius + world.GetTerrainHeightFromDirCcf(at, accurate: true);
                string pad = PadSurface.TryRadius(world, at, terrain, out double top) ? $"{(top - feetR) * 100:F0}" : "-";
                profile.Add($"{m}m t{(terrain - feetR) * 100:F0} p{pad}");
            }

            report["ground_left_cm"] = string.Join(" | ", profile);
            List<string> sites = [];
            double3 me = ToCcf(world, feetEcl);
            if (world.BodyTemplate is { } tpl)
            {
                foreach (LocationReference loc in tpl.Locations)
                {
                    if (loc is not LandmarkReference lm || lm.GetStaticObject() is not { } so) continue;
                    double far = Vec.Len(lm.ForwardCcf * Vec.Len(me) - me);
                    if (far < 2000) sites.Add($"{lm.Id} pad={lm.IsLaunchPad} {far:F0}m off={so.GroundOffset:F2} surf={so.SurfaceHeight:F2} foot={so.FootprintRadius:F0}");
                }
            }

            report["sites_near"] = sites;
        }
        if (_kitten is { Parent: Celestial under } feetOf)
        {
            // Where the kitten stands against where the ball would: the check that the two agree.
            double3 feet = ToCcf(under, KittenFrame.ToEcl(feetOf, Vec.Zero));
            if (new BodyGround(under).TrySurfaceRadius(Vec.Unit(feet), out double surface)) report["feet_above_ground_cm"] = Math.Round((Vec.Len(feet) - surface) * 100.0, 1);
        }
        if (_kitten is { } k && LookHeading() is { } heading)
        {
            double3 facing = KittenFrame.DirectionToEcl(k, new double3(0, 0, 1));
            report["drawn_turn_deg"] = Math.Round(double.RadiansToDegrees(_turnToLook), 1);
            double3 velocity = KsaWorld.VelocityEcl(k) - KsaWorld.GroundVelocityAt((Celestial)k.Parent, KsaWorld.PositionEcl(k));
            report["walk_speed_mps"] = Math.Round(Vec.Len(velocity), 2);
            report["walk_off_look_deg"] = Vec.Len(velocity) > 0.2 ? Math.Round(double.RadiansToDegrees(Vec.AngleBetween(Vec.RejectFrom(velocity, KsaWorld.PositionEcl(k) - ((Celestial)k.Parent).GetPositionEcl()), heading)), 0) : null;
            report["look_yaw_deg"] = Math.Round(double.RadiansToDegrees(_look.Yaw), 1);
            report["spin_dps"] = Math.Round(double.RadiansToDegrees(Vec.Len(k.BodyRates)), 0);
            report["facing_off_look_deg"] = Math.Round(double.RadiansToDegrees(Vec.AngleBetween(Vec.RejectFrom(facing, KsaWorld.PositionEcl(k) - ((Celestial)k.Parent).GetPositionEcl()), heading)), 1);
        }
        return report;
    }

    public void Cursor(double x, double y)
    {
        // The first position after the cursor is captured jumps to wherever the capture put it.
        if (_lastX is { } lastX && _lastY is { } lastY && Math.Abs(x - lastX) < 400.0 && Math.Abs(y - lastY) < 400.0)
        {
            if (Addressing) _dx += x - lastX;
            else _look.Turn(x - lastX, y - lastY);
        }

        _lastX = x;
        _lastY = y;
    }

    /// <summary>Draws the ball into one viewport, as its viewport is rendered, and in first person the club.</summary>
    public void Draw(IViewport viewport)
    {
        DrawViewClub(viewport);
        if (_ball is null || _ballBody is null || _ballMesh is null) return;

        double3 ego = BallEcl() - viewport.GetCamera().PositionEcl;
        if (!Vec.IsFinite(ego) || Vec.Len(ego) > 5000.0) return;

        doubleQuat turned = doubleQuat.Concatenate(_ball.Spin, _ballBody.GetCcf2Cce());
        _ballMesh.Transform = float4x4.CreateScale(0.01f)
                              * float4x4.CreateFromQuaternion(floatQuat.Pack(turned))
                              * float4x4.CreateTranslation(float3.Pack(ego));
        _ballMesh.Draw(Program.Instance.SuperMeshRenderSystem.ViewForViewport(viewport));
    }

    private void DrawViewClub(IViewport viewport)
    {
        if (!ReferenceEquals(viewport, Program.MainViewport) || !_config.FirstPerson || !_borrowed || _phase != Phase.Addressing) return;
        if (_kitten is not { } kitten || !KsaWorld.IsAlive(kitten)) return;

        _viewClub ??= AttachmentMesh.TryBuild("KSAGolf_Putter_Glb", "KSAGolf_Putter_Material", out StaticMeshRenderable? built, out _) ? built : null;
        if (_viewClub is null) return;

        _viewClub.Transform = GolfPose.ClubInModel(_rig, Posture(), _stroke.Theta) * KittenFrame.ModelToEgo(kitten, viewport.GetCamera().PositionEcl);
        _viewClub.Draw(Program.Instance.SuperMeshRenderSystem.ViewForViewport(viewport));
    }

    // ---- the club -------------------------------------------------------------------------------

    private void Hold(KittenEva kitten)
    {
        if (KittenPoseProbe.AvatarOf(kitten) is not { } avatar) return;
        if (!AttachmentMesh.TryBuild("KSAGolf_Putter_Glb", "KSAGolf_Putter_Material", out StaticMeshRenderable? club, out string why))
        {
            Status = "The putter could not be loaded.";
            Log.Warn($"the putter cannot be held: {why}");
            return;
        }

        int wrist = avatar.Core.CharacterModel.Skeleton.BoneNames?.IndexOf("Wrist_R") ?? -1;
        if (wrist < 0)
        {
            club!.Dispose();
            return;
        }

        avatar.Attachments.CosmeticAttachments.Add(new CharacterAvatar.CosmeticAttachment
        {
            Mesh = club,
            SocketIndex = wrist,
            Transform = float4x4.CreateScale((float)_rig.Scale),
        });

        _pose = new GolfPose(kitten, avatar, club!, _rig, Posture);
        avatar.Core.CharacterModel.AnimProcessors.Add(_pose);

        // The MMU's hand controllers ride the forearms and cover the paws holding the grip, and a round of
        // golf is played bareheaded.
        _mmuWas = avatar.Attachments.Mmu.Enabled;
        avatar.Attachments.Mmu.Enabled = false;
        _helmetWas = avatar.Attachments.Helmet.Enabled;
        avatar.Attachments.Helmet.Enabled = false;

        _kitten = kitten;
        _avatar = avatar;
        _club = club;
        _phase = Phase.Carrying;
        Status = _ball is null ? "Drop a ball, then press G near it." : "Walk up to the ball and press G.";
        Log.Info($"{KsaWorld.DisplayName(kitten)} picked up the putter");
    }

    private void LetGo()
    {
        if (_phase != Phase.Carrying) Leave("");
        FirstPersonHook.Body = null;
        FirstPersonHook.Heading = null;
        if (_borrowed)
        {
            KsaWorld.TryHandBackMainView(_saved, _kitten, out _, out _);
            _borrowed = false;
        }

        if (_avatar is { } avatar)
        {
            avatar.Attachments.CosmeticAttachments.RemoveAll(a => ReferenceEquals(a.Mesh, _club));
            if (_pose is not null) avatar.Core.CharacterModel.AnimProcessors.Remove(_pose);
            avatar.Attachments.Mmu.Enabled = _mmuWas;
            avatar.Attachments.Helmet.Enabled = _helmetWas;
        }

        _club?.Dispose();
        _club = null;
        _pose = null;
        _avatar = null;
        _kitten = null;
        _blend = 0.0;
    }

    // ---- addressing the ball ---------------------------------------------------------------------

    private void TryAddress()
    {
        if (_kitten is not { } kitten || kitten.Parent is not Celestial body)
        {
            Status = "Fly a kitten on the ground to putt.";
            return;
        }

        if (_ball is null || !ReferenceEquals(_ballBody, body)) DropBall();
        if (_ball is not { } ball) return;

        if (!ball.Resting)
        {
            Status = "Wait for the ball to stop.";
            return;
        }

        // The target is to the kitten's left as it stands -- in first person, the left of where the player
        // looks -- so stepping up to the ball never turns it or the view.
        _upCcf = Vec.Unit(ball.PositionCcf);
        double3 left = KittenFrame.DirectionToEcl(kitten, new double3(1, 0, 0));
        if (_config.FirstPerson && LookHeading() is { } heading && TryLocalFrame(kitten, out double3 vertical, out _, out _))
        {
            left = Vec.Cross(vertical, heading);
        }

        _targetCcf = Vec.Unit(Vec.RejectFrom(ToCcfDirection(body, left), _upCcf));

        // The third-person view is held where the player left it, against the kitten and turning with the ground.
        if (Program.GetMainCamera() is { } camera)
        {
            _viewOffsetCcf = ToCcfDirection(body, camera.PositionEcl - KsaWorld.PositionEcl(kitten));
            _viewForwardCcf = ToCcfDirection(body, camera.GetForwardEcl());
        }

        double3 ballModel = KittenFrame.FromEcl(kitten, BallEcl());
        double3 targetModel = KittenFrame.DirectionFromEcl(kitten, ToEclDirection(body, _targetCcf));
        if (!Stance.TryFor(ballModel, targetModel, _rig, StepReachCm, out Stance stance))
        {
            Status = $"The ball is {BallDistance ?? 0.0:F1} m away. Walk up to it.";
            return;
        }

        _stance = stance;
        _standingCcf = ToCcf(body, KsaWorld.PositionEcl(kitten));
        _stroke.Reset();
        _dx = 0.0;
        _lastX = null;
        _lastY = null;
        _sinceStrike = -1.0;
        _sinceAddress = 0.0;
        _phase = Phase.Addressing;

        // Which way the target lies on screen decides which way the mouse hits.
        _towards = 1.0;
        if (TryView(KsaWorld.PositionEcl(kitten), out _, out double3 forward, out double3 up))
        {
            double3 right = Vec.Cross(forward, up);
            _towards = Vec.Dot(right, ToEclDirection(body, _targetCcf)) >= 0.0 ? 1.0 : -1.0;
        }

        (string back, string through) = _towards > 0.0 ? ("left", "right") : ("right", "left");
        Status = $"Move the mouse {back} to take the club back, then {through} to hit. G steps away.";
        Log.Info($"addressing the ball: stepping {Vec.Len(stance.Offset):F0} cm, turning {double.RadiansToDegrees(stance.Yaw):F0} deg");
    }

    private void Swinging(double dtPlayer)
    {
        if (_kitten is not { } kitten || _ball is not { } ball || _ballBody is null)
        {
            Leave("");
            return;
        }

        double dx = _dx;
        _dx = 0.0;

        // The follow-through: the club still moves, and the ball it has struck rolls away.
        if (_sinceStrike >= 0.0)
        {
            _stroke.Update(dx, dtPlayer, _towards, _rig, out _);
            _sinceStrike += dtPlayer;
            if (ball.Resting || _sinceStrike >= WatchSeconds) Leave($"{Status} It rolled {Distance.Say(Vec.Len(ball.PositionCcf - _struckFromCcf))}.");
            return;
        }

        // Walked or knocked off: the kitten drawn at the ball has left its body behind.
        if (Vec.Len(ToCcf(_ballBody, KsaWorld.PositionEcl(kitten)) - _standingCcf) > DriftM || !ball.Resting)
        {
            Leave("You moved away from the ball. Press G to step up again.");
            return;
        }

        _sinceAddress += dtPlayer;
        if (KsaWorld.IsPaused || _sinceAddress < SettleSeconds) return;

        if (_stroke.Update(dx, dtPlayer, _towards, _rig, out double speed))
        {
            _struckFromCcf = ball.PositionCcf;
            ball.Strike(_targetCcf * speed);
            GolfSounds.Play(StrikeSound.ForStrike(speed), kitten, BallEcl());
            _sinceStrike = 0.0;
            Status = $"Struck at {speed:F2} m/s.";
            Log.Info($"struck the ball at {speed:F2} m/s (swing {double.RadiansToDegrees(_stroke.Rate):F0} deg/s)");
        }
    }

    private void Leave(string status)
    {
        _phase = Phase.Carrying;
        _stroke.Reset();
        _sinceStrike = -1.0;
        Status = status;
    }

    public bool TryPose(double3 followedEcl, out double3 offsetFromFollowed, out double3 forwardEcl, out double3 upEcl, out double fovDeg)
    {
        fovDeg = Fov;
        return TryView(followedEcl, out offsetFromFollowed, out forwardEcl, out upEcl);
    }

    // The view, measured against the followed kitten's position as the engine hands it in: through the
    // kitten's eyes, or at address from in front of it.
    private bool TryView(double3 followedEcl, out double3 offset, out double3 forward, out double3 up)
    {
        offset = forward = up = default;
        if (_config.FirstPerson) return TryEyes(followedEcl, out offset, out forward, out up);
        if (_kitten is not { } kitten || kitten.Parent is not Celestial body) return false;

        // As the player left it when they stepped up to the ball.
        offset = ToEclDirection(body, _viewOffsetCcf);
        forward = Vec.Unit(ToEclDirection(body, _viewForwardCcf));
        up = ToEclDirection(body, _upCcf);
        if (Math.Abs(Vec.Dot(forward, up)) > 0.98) up = ToEclDirection(body, _targetCcf);
        return Vec.IsFinite(offset) && Vec.IsFinite(forward);
    }

    private bool TryEyes(double3 followedEcl, out double3 offset, out double3 forward, out double3 up)
    {
        offset = forward = up = default;
        if (_kitten is not { } kitten || _pose?.Head is not { } head || !TryLocalFrame(kitten, out up, out double3 north, out double3 east)) return false;

        // Ahead of the head along the look rather than along the body, which lags a hard turn: the view
        // then pivots about the head as a head turns, instead of swinging round it. Measured against the
        // kitten's own position, with the one the engine hands in: both this frame's.
        double3 heading = (north * Math.Cos(_look.Yaw)) + (east * Math.Sin(_look.Yaw));
        double3 headEcl = KittenFrame.ToEcl(kitten, head) - KsaWorld.PositionEcl(kitten) + followedEcl;
        double3 eye = headEcl + (heading * (EyeFromHead.Z / 100.0)) + (up * (EyeFromHead.Y / 100.0));
        offset = eye - followedEcl;

        // Where the player looks. Stepping up to the ball nods the view down to it as the kitten steps,
        // and back up as it steps away -- a nod and nothing else, so the heading never moves; the mouse
        // swings the club meanwhile.
        double pitch = _look.Pitch;
        if (_blend > 0.0 && _ball is not null && _ballBody is not null)
        {
            double3 toBall = BallEcl() - eye;
            double down = -Vec.Dot(toBall, up);
            double ahead = Math.Max(Vec.Dot(toBall, heading), 0.05);
            double atBall = Math.Max(-Math.Atan2(down, ahead), -HeadLook.MaxPitchRad);
            pitch += (atBall - pitch) * _blend;
        }

        forward = Vec.Unit((heading * Math.Cos(pitch)) + (up * Math.Sin(pitch)));

        // The view's own up, tilted with it: square to the view at any pitch, so looking straight down
        // and turning spins the picture about its middle instead of rolling it.
        up = Vec.Unit((up * Math.Cos(pitch)) - (heading * Math.Sin(pitch)));

        return Vec.IsFinite(offset) && Vec.IsFinite(forward);
    }

    // ---- the ball --------------------------------------------------------------------------------

    private void StepBall(double dtSim)
    {
        if (_ball is not { } ball || _ballBody is not { } body || !(dtSim > 0.0)) return;

        // Beyond this a warped step is not worth integrating faithfully: the ball simply lags.
        double dt = Math.Min(dtSim, 2.0);
        int impacts = ball.Impacts;
        bool resting = ball.Resting;
        CourseSurface? course = CourseOn(body);
        ball.Step(dt, GroundOn(body), ((IParentBody)body).Mu, KsaWorld.MediumDensityRatioAt(body, BallEcl()), course);

        if (ball.Impacts != impacts && _kitten is { } near) GolfSounds.Play(StrikeSound.ForLanding(ball.LastImpactSpeed), near, BallEcl());
        if (!resting && ball.Resting && course is not null && course.InCup(ball.PositionCcf))
        {
            Status = "In the cup!";
            Log.Info("the ball dropped into the cup");
        }
    }

    private double3 BallEcl() => _ballBody!.GetPositionEcl() + double3.Transform(_ball!.PositionCcf, _ballBody.GetCcf2Cce());

    private static double3 ToCcf(Celestial body, double3 ecl) => double3.Transform(ecl - body.GetPositionEcl(), body.GetCce2Ccf());

    private static double3 ToCcfDirection(Celestial body, double3 ecl) => double3.Transform(ecl, body.GetCce2Ccf());

    private static double3 ToEclDirection(Celestial body, double3 ccf) => double3.Transform(ccf, body.GetCcf2Cce());

    // The ground under the ball: the engine's height field, with the sea standing in for the seabed, and a
    // launch site's top wherever one stands on it (PadSurface).
    internal sealed class BodyGround(Celestial body) : IBodyGround
    {
        public bool TrySurfaceRadius(double3 dirCcf, out double radius)
        {
            radius = 0.0;
            try
            {
                double height = body.GetTerrainHeightFromDirCcf(dirCcf, accurate: true);
                bool hasSea = body.GetOceanReference() is { } sea && sea.Density > 0.0;
                double seaLevel = hasSea ? body.GetOceanReference()!.Level : 0.0;

                radius = body.MeanRadius + GroundSurface.Height(height, seaLevel, hasSea);
                if (PadSurface.TryRadius(body, dirCcf, radius, out double top)) radius = Math.Max(radius, top);

                return double.IsFinite(radius) && radius > 0.0;
            }
            catch
            {
                return false;
            }
        }
    }
}
