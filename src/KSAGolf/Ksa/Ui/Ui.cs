using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KSAGolf;

/// <summary>
/// The panel: putting, the world clock, the developer tools, and the log switches.
/// </summary>
internal sealed class Ui(Config config, CraftMover mover, Golf golf, CourseBuilder builder)
{
    private static readonly float4 Green = new(0.4f, 1.0f, 0.45f, 1f);
    private static readonly float4 Amber = new(1.0f, 0.78f, 0.25f, 1f);

    private readonly Config _config = config;
    private readonly CraftMover _mover = mover;
    private readonly Golf _golf = golf;
    private readonly CourseBuilder _builder = builder;

    public bool Visible = true;

    /// <summary>
    /// Opens the main menu bar before KSA fills it, so this mod's entry sits alongside KSA's.
    ///
    /// <para>Called from <b>before</b> KSA's GUI pass: from after it, <c>BeginMainMenuBar</c> returns
    /// false because the bar has already been ended for the frame.</para>
    /// </summary>
    public void DrawMenuBarEntry()
    {
        // ModMenu draws this mod's entry when it is installed; a second would list it twice.
        if (ModMenuPresence.Installed) return;

        try
        {
            if (!ImGui.BeginMainMenuBar()) return;

            if (ImGui.BeginMenu("KSAGolf"))
            {
                DrawMenuContents();
                ImGui.EndMenu();
            }

            ImGui.EndMainMenuBar();
        }
        catch (Exception e)
        {
            // An exception here happens inside KSA's own GUI pass; a missing menu is only cosmetic.
            if (_warnedMenuBar) return;

            _warnedMenuBar = true;
            Log.Warn($"menu bar entry failed, use the floating button: {e.Message}");
        }
    }

    private bool _warnedMenuBar;

    private void DrawMenuContents()
    {
        bool visible = Visible;
        if (ImGui.MenuItem("Panel", default, ref visible, true)) Visible = visible;
    }

    // Said once rather than per frame: this runs inside a menu build, so a failure repeats while it is open.
    private static bool _warnedModMenu;

    /// <summary>
    /// Called by <b>ModMenu</b>, if the player has it, to fill this mod's entry in its shared menu.
    /// Static because ModMenu resolves an instance only for a couple of hardcoded method names.
    /// </summary>
    [ModMenuEntry("KSAGolf")]
    public static void DrawModMenu()
    {
        try
        {
            Current?.DrawMenuContents();
        }
        catch (Exception e)
        {
            if (_warnedModMenu) return;
            _warnedModMenu = true;
            Log.Warn($"ModMenu entry failed, so its Panel item will not work: {e.Message}. "
                     + "Reopen from the floating KSAGolf button instead.");
        }
    }

    /// <summary>The panel ModMenu should drive. Null until the first draw, which ModMenu may scan before.</summary>
    internal static Ui? Current { get; private set; }

    public void Draw()
    {
        Current = this;
        if (_builder.Active) DrawBuilder();

        if (Visible)
        {
            // ###id so the version can ride in the title without the window losing its place.
            if (ImGui.Begin($"KSAGolf {Build.Version}###KSAGolf", ref Visible))
            {
                if (Build.Developer)
                {
                    DrawCaptureForClaude();
                    ImGui.Separator();
                }

                DrawGolf();
                ImGui.Separator();
                DrawWorldClock();
                ImGui.Separator();
                DrawCraftMover();
                DrawSendToBody();
                ImGui.Separator();
                DrawLogging();
            }

            ImGui.End();
        }
        else if (_config.FloatingPanelButton)
        {
            // Closing the panel must not strand the player: both other routes back are somebody
            // else's, and the setting that would switch this on lives inside the shut panel.
            if (ImGui.Begin("KSAGolf##reopen", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
            {
                if (ImGui.Button("KSAGolf")) Visible = true;
            }

            ImGui.End();
        }
    }

    // A capture of what the player is looking at, with the state and the log at that moment.
    private static void DrawCaptureForClaude()
    {
        if (ImGui.Button("Capture for Claude")) Bridge.RequestPlayerCapture();
        Tip("Saves eight frames of what is on screen with a note of the game's state and the end of "
            + "the log, beside the log in bridge/out. Nothing is sent anywhere.");

        if (Bridge.LastPlayerCapture is { } last) ImGui.TextDisabled($"  saved {last}");
    }

    private void DrawGolf()
    {
        ImGui.Checkbox("Play golf", ref _config.PlayGolf);
        Tip("The kitten you fly carries a putter. Drop a ball, walk up to it and press G: the kitten steps "
            + "up beside the ball to hit it to its left, the view stays where it is, and the mouse swings "
            + "the club. Which way takes it back and which way hits is said below. G again steps away.");

        if (!_config.PlayGolf) return;

        if (!_golf.Holding)
        {
            ImGui.TextDisabled("  fly a kitten out on the ground to pick up the putter");
            return;
        }

        if (ImGui.Button(_builder.Active ? "Stop building (B)" : "Build a course (B)")) _builder.Toggle();
        Tip("Look straight down on a grid in front of the kitten and lay a minigolf hole out of pieces, from a "
            + "tee to a cup. The kitten stays where it is.");
        if (!_builder.Active && _builder.Status.Length > 0 && _builder.Course.Pieces.Count == 0) ImGui.TextDisabled($"  {_builder.Status}");

        ImGui.Checkbox("First-person view (V)", ref _config.FirstPerson);
        Tip("See through the kitten's eyes. The mouse looks around and the kitten faces where you look, so "
            + "left and right step sideways. Hold Alt for the cursor. At address it looks down at the ball.");

        if (ImGui.Button(_golf.HasBall ? "Drop a new ball" : "Drop a ball")) _golf.DropBall();
        Tip("Puts a ball down in front of the kitten, replacing the one in play.");

        if (_golf.HasBall)
        {
            ImGui.SameLine();
            if (ImGui.Button("Pick up the ball")) _golf.PickUpBall();

            ImGui.SameLine();
            if (ImGui.Button(_golf.Addressing ? "Step away (G)" : "Address (G)")) _golf.AddressOrStepAway();

            if (_golf.BallDistance is { } away)
            {
                ImGui.TextDisabled($"  ball {Distance.Say(away)} away{(_golf.BallResting ? "" : ", rolling")}");
            }
        }

        if (_golf.Addressing) ImGui.TextColored(Green, $"  club {_golf.SwingDeg:+0;-0} deg");
        if (_golf.Status.Length > 0) ImGui.TextColored(Amber, $"  {_golf.Status}");
    }

    private void DrawBuilder()
    {
        if (ImGui.Begin("Course builder###KSAGolfBuilder", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextDisabled("Pieces");
            Tip("Click a piece, then click the grid to lay it. Near an open end it snaps on. Keys: 1-9 choose, "
                + "0 picks pieces up to move them, R turns, M mirrors, PgUp/PgDn change level, X or Del removes, "
                + "Ctrl+Z / Ctrl+Y undo and redo. WASD or a right or middle drag pans, the wheel zooms, Q/E turn "
                + "the view. B leaves.");

            if (ImGui.RadioButton("Move (0)", _builder.Selected < 0)) _builder.Select(-1);
            for (int n = 0; n < PieceCatalogue.All.Length; n++)
            {
                PieceDef def = PieceCatalogue.All[n];
                if (n % 3 != 2) ImGui.SameLine();
                if (ImGui.RadioButton($"{def.Name}{(n < 9 ? $" ({n + 1})" : "")}###piece{n}", _builder.Selected == n)) _builder.Select(n);
            }

            ImGui.TextDisabled($"  turned {_builder.Orientation.Quarter * 90} deg{(_builder.Orientation.Mirrored ? ", mirrored" : "")}, level {_builder.Level}");
            ImGui.Separator();

            ImGui.TextColored(_builder.Report.Playable ? Green : Amber, _builder.Status);

            if (ImGui.Button("Undo") && _builder.CanUndo) _builder.Undo();
            ImGui.SameLine();
            if (ImGui.Button("Redo") && _builder.CanRedo) _builder.Redo();
            ImGui.SameLine();
            if (ImGui.Button("Clear")) _builder.Clear();
            ImGui.SameLine();
            if (ImGui.Button("Sample hole")) _builder.LoadSample();
            Tip("Replaces the course with a hole built from every kind of piece.");

            ImGui.Separator();
            if (ImGui.Button(_builder.Name.Length > 0 ? $"Save {_builder.Name}" : "Save")) _builder.Save();
            foreach (string saved in CourseBuilder.Saved())
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Load {saved}")) _builder.Load(saved);
            }

            if (ImGui.Button("Done (B)")) _builder.Leave();
        }

        ImGui.End();
    }

    // KSA's own roller stops at 0.1x; these go two decades below.
    private static readonly (string Label, double Speed)[] SlowMotionSpeeds =
    [
        ("0.01x", 0.01), ("0.05x", 0.05), ("0.1x", 0.1), ("0.25x", 0.25), ("1x", 1.0),
    ];

    private static void DrawWorldClock()
    {
        ImGui.Text($"Sim speed: {KsaWorld.SimulationSpeed:0.###}x");

        foreach ((string label, double speed) in SlowMotionSpeeds)
        {
            ImGui.SameLine();
            if (ImGui.Button(label)) KsaWorld.SetSimulationSpeed(speed);
        }
    }

    private void DrawCraftMover()
    {
        ImGui.Checkbox("Move craft with the mouse", ref _config.MoveCraftWithMouse);
        Tip("On: click a craft to lift it, then click the ground to set it down.");

        if (!_config.MoveCraftWithMouse) return;

        if (_mover.Held is { } held)
        {
            ImGui.TextColored(Amber, $"  holding {KsaWorld.DisplayName(held)} - click the ground");
            ImGui.SameLine();
            if (ImGui.SmallButton("Cancel")) _mover.Release();
        }
        else if (_mover.Hovered is { } over)
        {
            ImGui.TextColored(Green, $"  click to pick up {KsaWorld.DisplayName(over)}");
        }
        else
        {
            ImGui.TextDisabled("  point at a craft; it rings when the click would take it");
        }
    }

    private readonly List<(string Id, bool HasAir)> _bodies = [];

    private void DrawSendToBody()
    {
        if (KsaWorld.ControlledVehicle is not { } craft)
        {
            ImGui.TextDisabled("Send to another body: nothing is being flown.");
            return;
        }

        KsaWorld.SystemBodies(_bodies);
        if (_bodies.Count == 0)
        {
            ImGui.TextDisabled("Send to another body: no system loaded.");
            return;
        }

        ImGui.Text("Send the controlled craft to");
        Tip("Sets it down at 0, 0 on that body. The mouse mover can only reach the body you are "
            + "already looking at.");

        for (int i = 0; i < _bodies.Count; i++)
        {
            (string id, bool hasAir) = _bodies[i];

            if (i > 0) ImGui.SameLine();

            if (ImGui.SmallButton(id))
            {
                if (KsaWorld.TryPlaceOnSurface(craft, id, 0.0, 0.0))
                    Log.Info($"placed {KsaWorld.DisplayName(craft)} on {id} at 0, 0");
                else
                    Log.Warn($"could not place {KsaWorld.DisplayName(craft)} on {id}");
            }

            Tip(hasAir ? "Has an atmosphere." : "Airless.");
        }
    }

    private void DrawLogging()
    {
        if (ImGui.Checkbox("Verbose log", ref _config.VerboseLog))
        {
            Log.Threshold = _config.VerboseLog ? Log.Level.Debug : Log.Level.Info;
            Log.Info(_config.VerboseLog ? "verbose logging on" : "verbose logging off");
        }
        Tip("More detail in the log, which is what a bug report wants.");
        ImGui.TextDisabled("  -> Logs/KSAGolf.log");
    }

    // What the control just drawn does, on hover. Wrapped, because a bare tooltip never is.
    private static void Tip(string text)
    {
        if (!ImGui.BeginItemTooltip()) return;

        ImGui.PushTextWrapPos(ImGui.GetFontSize() * TipWidthEms);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // The width Dear ImGui's own help markers wrap at.
    private const float TipWidthEms = 35f;
}
