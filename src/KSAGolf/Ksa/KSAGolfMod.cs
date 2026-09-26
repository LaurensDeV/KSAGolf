using KSA;
using StarMap.API;

namespace KSAGolf;

/// <summary>
/// StarMap entry point. StarMap loads the assembly named by mod.toml's EntryAssembly, instantiates
/// the first type carrying <see cref="StarMapModAttribute"/>, and dispatches to the attributed
/// methods below.
///
/// <para>Frame work is wrapped so a fault degrades the mod instead of taking the game down, and
/// repeated faults disable it rather than filling the log.</para>
/// </summary>
[StarMapMod]
public sealed class KSAGolfMod
{
    private const int FaultLimit = 10;

    private readonly Config _config = new();
    private readonly FrameLatch _frame = new();
    private readonly CraftMover _mover = new();

    private double _lastSimSpeed = 1.0;
    private double _lastSimStep;
    private Ui? _ui;
    private Bridge? _bridge;
    private int _faults;
    private bool _disabled;

    [StarMapImmediateLoad]
    public void OnImmediateLoad(Mod mod)
    {
        Log.Info($"loading (mod id: {mod.Id})");
        Log.Info($"KSAGolf {Build.Version} built for KSA {Build.KsaBuild ?? "?"}, running {Build.KsaRunning ?? "?"}");
    }

    [StarMapAllModsLoaded]
    public void OnFullyLoaded()
    {
        // Steps a frame the GUI pass skipped before the render rather than after it. A refusal
        // degrades to the frame postfix, so it costs a frame and nothing else.
        PreRenderHook.Install(StepOnce);

        // The only way to see a save load: StarMap has no hook for one, and the mod never leaves the
        // flight scene across it.
        WorldReloadHook.Install();

        if (Build.Developer) _bridge = new Bridge(_config);

        Log.Info(Build.Developer
                     ? "developer install: the bridge and the developer controls are on"
                     : "player install");

        _ui = new Ui(_config, _mover);
        Log.Info("ready - open the 'KSAGolf' panel");
    }

    /// <summary>
    /// The frame postfix: the fallback step, and the bridge.
    ///
    /// <para>StarMap passes a <em>player-time</em> delta, and nothing that moves with the world may
    /// integrate it: player time runs through a pause and ignores timewarp. The simulation clock is
    /// <see cref="KsaWorld.ConsumeSimStep"/>.</para>
    /// </summary>
    [StarMapAfterOnFrame]
    public void OnAfterFrame(double currentPlayerTime, double dtPlayer)
    {
        try
        {
            if (_disabled || _ui is null) return;

            if (KsaWorld.InFlight)
            {
                double speed = KsaWorld.SimulationSpeed;
                if (Math.Abs(speed - _lastSimSpeed) > 1e-9)
                {
                    Log.Info($"simulation speed {_lastSimSpeed:F2}x -> {speed:F2}x"
                             + (KsaWorld.IsPaused ? " (paused)" : ""));
                    _lastSimSpeed = speed;
                }
            }

            // A no-op on every frame the GUI pass ran. See StepOnce.
            StepOnce(dtPlayer);

            // After the step, so a command sees this frame's world and its simulated step.
            _bridge?.Update(dtPlayer, KsaWorld.InFlightScene && !KsaWorld.IsPaused ? _lastSimStep : 0.0);
        }
        catch (Exception e)
        {
            Fault("frame", e);
        }
        finally
        {
            // Unconditionally: this is the only hook KSA always calls, so a release skipped here stops
            // the mod for the session rather than for a frame.
            _frame.EndFrame();
        }
    }

    // One simulation step, from whichever hook reaches it first this frame. The GUI pass is preferred
    // because stepping there is what pairs a body's offset with the anchor it is drawn against
    // (docs/FRAMES-AND-EPOCHS.md); F2 skips that pass, so the frame postfix is the fallback.
    private void StepOnce(double dtPlayer)
    {
        if (_disabled || _ui is null) return;
        if (!_frame.Claim()) return;

        KsaWorld.BeginFrame();

        if (WorldReloadHook.ConsumeReload()) ForgetTheWorld();

        // The scene, not the craft: KSA clears the controlled vehicle when it is destroyed and the
        // flight carries straight on.
        if (KsaWorld.InFlightScene)
        {
            _lastSimStep = KsaWorld.ConsumeSimStep();
        }

        // Driving a borrowed view is not drawing, so it runs with the step and not with the panel.
        _bridge?.DriveCamera();
    }

    // Drops everything held about a world a save load has just replaced; the scene never ends across
    // one, so nothing else would.
    private void ForgetTheWorld()
    {
        Log.Info("a save was loaded - forgetting the previous world");
        _mover.Release();
        KsaWorld.ResetSimStepTracking();
    }

    /// <summary>
    /// Opens the main menu bar before KSA fills it, so this mod's entry sits beside File and Universe.
    /// </summary>
    [StarMapBeforeGui]
    public void OnBeforeGui(double dt)
    {
        if (_disabled || _ui is null) return;

        try { _ui.DrawMenuBarEntry(); }
        catch { /* Cosmetic. Never take KSA's GUI pass down for a menu item. */ }
    }

    /// <summary>
    /// The panel and anything drawn in the world. Gizmos have to be submitted here, a postfix on
    /// <c>OnDrawUiViewports</c>: it sits between the frame's gizmo reset and the render, where the
    /// frame postfix lands after the render and is cleared before it is drawn.
    /// </summary>
    [StarMapAfterGui]
    public void OnAfterGui(double dt)
    {
        if (_disabled || _ui is null) return;

        try
        {
            // First, so the drawing below reads the state this frame produced.
            StepOnce(dt);

            _ui.Draw();

            // After the panel, so a click on a window is not also a click on the world behind it.
            if (KsaWorld.InFlight)
            {
                _mover.Update(_config);
                _mover.Draw(_config);
            }
        }
        catch (Exception e)
        {
            Fault("gui", e);
        }
    }

    [StarMapUnload]
    public void Unload()
    {
        _mover.Release();
        _bridge?.Release();

        // The mod's own camera controller would otherwise outlive it for the rest of the session.
        KsaWorld.RestoreStockController();

        PreRenderHook.Remove();
        WorldReloadHook.Remove();
        KsaWorld.ResetSimStepTracking();
        _ui = null;
        Log.Info("unloaded");

        // Last: the log batches its writes, so without this the tail of the session never reaches disk.
        Log.Shutdown();
    }

    private void Fault(string where, Exception e)
    {
        _faults++;
        Log.Error($"{where} failed ({_faults}/{FaultLimit})", e);

        if (_faults < FaultLimit) return;

        _disabled = true;
        _mover.Release();
        PreRenderHook.Remove();
        WorldReloadHook.Remove();
        Log.Error("too many faults - KSAGolf disabled for this session");
    }
}
