using System.IO;
using System.Reflection;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;
using KSA.Rendering.Water.Data;

namespace KSAGolf;

/// <summary>
/// Every direct touch of KSA's internals lives here. KSA is pre-release and its API moves,
/// so keeping the surface in one file means a game update breaks one place, not ten.
///
/// All positions and velocities are in the ecliptic frame (Ecl): inertial, metres, and the
/// frame both <see cref="Vehicle.GetPositionEcl"/> and gizmo rendering agree on.
/// </summary>
internal static class KsaWorld
{
    /// <summary>The vehicle the player is currently flying, or null in menus.</summary>
    public static Vehicle? ControlledVehicle => Program.ControlledVehicle;

    /// <summary>
    /// The player has a craft to fly.
    ///
    /// <para><b>This is not "the flight scene is up", and using it as one strands the camera.</b>
    /// <c>Universe.DestroyVehicle</c> clears <c>ControlledVehicle</c> when the craft being flown is
    /// destroyed, and the scene carries straight on — the engine points the view at the wreckage
    /// and keeps rendering. Anything handing the player's view back must ask
    /// <see cref="InFlightScene"/>, or losing a craft is mistaken for leaving flight and the
    /// hand-back is skipped in the one case that most needs it.</para>
    /// </summary>
    public static bool InFlight => Program.ControlledVehicle is { IsDisposed: false };

    /// <summary>
    /// The flight scene is up, whether or not the player has a craft in it.
    ///
    /// <para>What separates a destroyed craft — still in flight, still one live camera, and
    /// everything the mod borrowed still worth handing back — from actually leaving, which is the
    /// editor or the menus. The new scene brings its own camera, so there the recording describes
    /// something that has gone.</para>
    /// </summary>
    public static bool InFlightScene
    {
        get
        {
            try
            {
                return Program.Editor is null && Universe.CurrentSystem is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// The simulated seconds KSA's last step actually advanced the world by.
    ///
    /// <para>This — not the player-time delta StarMap hands the frame hook, and not a
    /// difference of clock samples — is what the mod steps on. A paused game reports zero
    /// and a warped one reports the real span, and because it is the step the engine applied
    /// rather than one measured around it, it cannot be a step out of phase with the world.
    /// See <see cref="SimClock"/> for why that distinction is worth tens of metres.</para>
    /// </summary>
    public static double SimStepSeconds => Universe.GetLastSimStep().DeltaTime;

    // Pure, and in Sim/ so it can be tested. See StepGate.
    private static readonly StepGate<UniverseTime> _stepGate = new();

    /// <summary>
    /// The simulated seconds to integrate now, or zero if the engine has applied no new step
    /// since the last call. <b>Consuming</b> — call once per update and use the result.
    ///
    /// <para><see cref="SimStepSeconds"/> reports the <em>last</em> step, not one since the
    /// previous call, so asking twice without the engine stepping returns it twice. Integrating
    /// it twice adds motion the world never made, and it compounds because it lands in
    /// <c>PositionEcl</c>.</para>
    /// </summary>
    public static double ConsumeSimStep()
    {
        SimStep step = Universe.GetLastSimStep();

        if (_consumedThrough is { } from && !step.NextTime.Equals(from))
        {
            LastLongerOfStepAndSpan = Math.Max(step.DeltaTime, Span(from, step.NextTime));
        }

        _consumedThrough = step.NextTime;

        // The span, not just the step: a frame in which this mod's hook never ran still advanced
        // the world, and GetLastSimStep reports only the most recent one. Taking a screenshot is
        // exactly that case -- ScreenshotCapture sets Program.DrawUI false and KSA guards the call
        // this mod postfixes with it, so the hook is not called at all. Differenced in Int128
        // nanoseconds because absolute universe time is far too large to subtract as double.
        return _stepGate.Consume(step.NextTime, step.DeltaTime, Span);
    }

    private static double Span(UniverseTime from, UniverseTime to)
        => (double)(to.Nanoseconds - from.Nanoseconds) / 1e9;

    private static UniverseTime? _consumedThrough;

    /// <summary>
    /// What the last new step would have been as the longer of the reported step and the span.
    /// Measurement only: summed beside the step taken it can be compared against
    /// <see cref="SimClockNanoseconds"/>, because the longer runs 0.125 ns a frame ahead of the
    /// clock the planets are placed on.
    /// </summary>
    public static double LastLongerOfStepAndSpan { get; private set; }

    /// <summary>Universe time in whole nanoseconds, which is the clock KSA places every celestial on.</summary>
    public static Int128 SimClockNanoseconds
    {
        get
        {
            try { return Universe.GetElapsedTime().Nanoseconds; }
            catch { return Int128.MinValue; }
        }
    }

    /// <summary>Forgets which step was last integrated. For unload and scene changes.</summary>
    public static void ResetSimStepTracking()
    {
        _stepGate.Reset();
        _consumedThrough = null;
    }

    /// <summary>True while the simulation is stopped. KSA defines this as speed exactly zero.</summary>
    public static bool IsPaused => Universe.IsPaused();

    /// <summary>Current timewarp factor; 1.0 is real time, 0.0 is paused. Display only.</summary>
    public static double SimulationSpeed => Universe.SimulationSpeed;

    /// <summary>
    /// Slowest speed worth offering. Below this KSA names the speed "paused" — its SimSpeed
    /// constructor calls anything under 1e-4 paused — and a world that runs while every label
    /// says it is stopped is worse than one that will not go slower.
    ///
    /// <para>It is only the *name*: Universe.IsPaused() tests the speed against exactly zero, so
    /// the world really would keep running. That mismatch is the trap, not a limit.</para>
    /// </summary>
    public const double SlowestSimSpeed = 0.001;

    /// <summary>Whether KSA is running its own warp-to-a-time, which it will not be interrupted during.</summary>
    public static bool IsAutoWarpActive => Universe.IsAutoWarpActive;

    /// <summary>
    /// Sets the world's simulation speed, including values slower than the in-game controls
    /// reach. KSA's own roller works in tenths, so 0.1x is as slow as it will go; nothing in
    /// the engine enforces that.
    ///
    /// <para><c>SetSimulationSpeed</c> only rejects speeds above <c>SimSpeed.MaxSpeed</c> — there
    /// is no floor — and it assigns the field directly rather than queuing an input event, so a
    /// value set here holds until something else changes it.</para>
    ///
    /// <para>Everything this mod does is already keyed to simulated time, so a slow world needs
    /// no special handling: the step simply comes back smaller and everything scales with it.</para>
    /// </summary>
    /// <returns>False if the value was not finite or not positive; the speed is left alone.</returns>
    public static bool SetSimulationSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0.0) return false;

        Universe.SetSimulationSpeed(new SimSpeed(Math.Max(speed, SlowestSimSpeed)));
        return true;
    }

    /// <summary>
    /// Stops the world, or starts it again at real time.
    ///
    /// <para>Separate from <see cref="SetSimulationSpeed"/>, which refuses anything at or below
    /// zero and clamps to <see cref="SlowestSimSpeed"/>: a caller asking for a slow world and a
    /// caller asking for a stopped one want different things, and a speed argument that silently
    /// becomes a pause is the trap that guard exists to close.</para>
    ///
    /// <para><c>Universe.IsPaused()</c> tests the speed against exactly zero, so this is what makes
    /// that property true — and with it <c>SimClock.Classify</c>'s paused verdict, which is how
    /// everything in this mod stops.</para>
    /// </summary>
    /// <returns>False only if the call threw.</returns>
    public static bool SetPaused(bool paused)
    {
        try
        {
            Universe.SetSimulationSpeed(new SimSpeed(paused ? 0.0 : 1.0));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True once the vehicle has been destroyed or unloaded.</summary>
    public static bool IsAlive(Vehicle? v) => v is { IsDisposed: false };

    /// <summary>
    /// Whether a craft is wreckage, as KSA marks the pieces <c>PartFailure.ShedDebris</c> breaks off.
    /// The craft they came off is not marked, so it stays a target until it is destroyed.
    /// </summary>
    public static bool IsDebris(Vehicle v)
    {
        try
        {
            return IsAlive(v) && v.IsDebris;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// How many vehicles share this one's physics bubble.
    ///
    /// <para>Read per craft rather than counted here because the bubble is what the engine
    /// actually budgets against.</para>
    /// </summary>
    public static int BubbleVehicleCount(Vehicle? v)
    {
        if (!IsAlive(v)) return -1;

        try { return v!.BubbleVehicleCount; }
        catch { return -1; }
    }

    /// <summary>
    /// Every live vehicle in the world, built at most once a frame and shared by everything that
    /// walks it.
    ///
    /// <para>Nothing about the answer is per caller: it is what exists, so walking the system once
    /// per consumer repeats the same work for the same result.</para>
    ///
    /// <para><b>Freshness is a generation, not a frame count.</b> The list may not outlive a
    /// change to the world, because a destroyed vehicle stays in it as a disposed reference where
    /// a fresh walk would simply omit it. So anything that removes a vehicle invalidates it, and
    /// each hook that can start a pass invalidates it once on the way in. Holding it across a
    /// frame is what <see cref="CollectVehicles"/>'s own warning is about, and this does not.</para>
    /// </summary>
    public static IReadOnlyList<Vehicle> Vehicles
    {
        get
        {
            if (_censusFresh) return _census;

            CollectVehicles(_census);
            _censusFresh = true;
            return _census;
        }
    }

    /// <summary>
    /// Throws the shared census away. Called at the top of each pass, and by anything that takes a
    /// vehicle out of the world.
    /// </summary>
    public static void InvalidateCensus() => _censusFresh = false;

    private static readonly List<Vehicle> _census = [];
    private static bool _censusFresh;

    /// <summary>
    /// Appends every vehicle the game currently has loaded into <paramref name="into"/>.
    ///
    /// Reads <c>Universe.CurrentSystem.All</c> rather than <c>Program.VehiclesInFrame</c>.
    /// The latter is a per-frame scratch buffer refilled by <c>RefreshVehiclesInFrame()</c>
    /// at a point in the tick that does not line up with a Harmony postfix on OnFrame - it
    /// reads back empty from there, which silently finds nothing. The system's collection
    /// is the authoritative list and is valid from any hook.
    ///
    /// Copies immediately: the result must not be held across frames, and engine state must not
    /// be under iteration while vehicles are being destroyed.
    /// </summary>
    public static void CollectVehicles(List<Vehicle> into)
    {
        into.Clear();

        try
        {
            if (Universe.CurrentSystem is { } system)
            {
                ReadOnlySpan<Astronomical> all = system.All.AsSpan();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Vehicle { IsDisposed: false } v) into.Add(v);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warn($"vehicle enumeration failed: {e.Message}");
        }

        // Belt and braces: if the system collection ever comes back empty, fall back to the
        // per-frame buffer rather than going blind.
        if (into.Count != 0) return;

        try
        {
            ReadOnlySpan<Vehicle> inFrame = Program.VehiclesInFrame;
            for (int i = 0; i < inFrame.Length; i++)
            {
                if (inFrame[i] is { IsDisposed: false } v) into.Add(v);
            }
        }
        catch
        {
            // Nothing more to try.
        }
    }

    public static double3 PositionEcl(Vehicle v) => v.GetPositionEcl();

    /// <summary>
    /// Where a body is, guarded — the anchor something in flight falls back on once the craft it
    /// left is gone. Non-finite rather than throwing, so a caller can decline to move anything this frame.
    /// </summary>
    public static double3 PositionEcl(Celestial body)
    {
        try
        {
            return body.GetPositionEcl();
        }
        catch
        {
            return new double3(double.NaN, double.NaN, double.NaN);
        }
    }

    public static double3 VelocityEcl(Vehicle v) => v.GetVelocityEcl();

    /// <summary>
    /// Whether a celestial body sits between two points — the planet in the way.
    ///
    /// <para>Every body in the system, not just the one being orbited: a marker hidden behind a
    /// moon is as unusable as one hidden behind the world under it.</para>
    /// </summary>
    /// <param name="blockedBy">The first body found in the way, or empty.</param>
    public static bool IsOccluded(double3 eyeEcl, double3 targetEcl, out string blockedBy)
        => IsOccluded(eyeEcl, targetEcl, 0.0, out blockedBy);

    /// <inheritdoc cref="IsOccluded(double3, double3, out string)"/>
    /// <param name="terrainMargin">
    /// Metres to inflate every body by, so a contact skimming the limb counts as hidden.
    /// </param>
    public static bool IsOccluded(double3 eyeEcl, double3 targetEcl, double terrainMargin,
                                  out string blockedBy)
    {
        blockedBy = string.Empty;
        try
        {
            if (Universe.CurrentSystem is not { } system) return false;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not Celestial body) continue;
                if (!LineOfSight.BlockedByTerrain(eyeEcl, targetEcl, body.GetPositionEcl(),
                                                  body.MeanRadius, terrainMargin))
                {
                    continue;
                }

                blockedBy = body.Id ?? string.Empty;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the skyline of the body under the eye hides the target, against the real height
    /// field.
    ///
    /// <para>Terrain only, and for one body. <see cref="IsOccluded"/> runs over every body in the
    /// system and stays in front of this: a ridge on another world only matters when that world is
    /// between the two points, which the sphere has already caught. So this is asked once the
    /// cheap rejects have all passed, and never for a contact they threw out.</para>
    /// </summary>
    /// <param name="samples">Height lookups this look may cost. Zero asks none.</param>
    // Each modifier contributes its amplitude times a weight, a lookup and a noise value all in
    // [0, 1], so the sum of the declared amplitudes is a supremum rather than an estimate. Earth's
    // total 7,525 m.
    private const double TerrainModifierHeadroomMetres = 8_000.0;

    // How high the terrain can possibly reach, as a bound the cheap reject may stand in front of
    // the exact test with.
    //
    // Celestial.MaxTerrainHeightApprox is NOT such a bound, and using it was a false negative. It
    // is computed in the Celestial constructor, before Universe.SetupRenderData populates the
    // modifiers, so erosion, dunes and detail contribute nothing -- and it samples a 16,384-point
    // Fibonacci spiral, about 176 km apart on Earth. Measured against the shipped height texture it
    // returns ~5,692 m where the base field alone reaches 8,011. A sightline six kilometres over the
    // Himalayas was declared unmasked without a single sample, which is the false negative
    // CLAUDE.md's "a sphere containing the terrain cannot produce a false negative" forbids.
    //
    // Astronomical.MaxTerrainRadius is exact for the base field, straight off the template. Over-
    // padding costs one thing: a contact high above the ground pays for samples it did not need.
    private static double MaxTerrainHeightMetres(Celestial body)
    {
        try
        {
            double exact = body.MaxTerrainRadius - body.MeanRadius;

            return double.IsFinite(exact) && exact > 0.0
                       ? exact + TerrainModifierHeadroomMetres
                       : body.MaxTerrainHeightApprox;
        }
        catch
        {
            return body.MaxTerrainHeightApprox;
        }
    }

    /// <summary>
    /// Where the cursor's ray first meets a celestial's ground, as a place a craft can be put or a
    /// ball can be sent.
    ///
    /// <para>Nearest body hit, not the one being orbited: pointing at a moon on the horizon should
    /// mean the moon. Walked out from the eye by <see cref="TerrainRay"/>, because the ray meets the
    /// ground where it first goes under it — the mean sphere's hit refined by the height under that
    /// answer lands behind a hill seen side-on, on the terrain beyond what the pointer is on.</para>
    /// </summary>
    public static bool TryCursorGroundPoint(out double3 groundEcl,
                                            out double latitudeDeg, out double longitudeDeg,
                                            out string bodyName)
    {
        groundEcl = default;
        latitudeDeg = 0.0;
        longitudeDeg = 0.0;
        bodyName = string.Empty;

        if (!TryCursorRayEcl(out double3 eye, out double3 direction)) return false;
        if (!TryGroundAlong(eye, direction, out groundEcl, out Celestial? nearest)) return false;

        try
        {
            double3 cce = groundEcl - nearest!.GetPositionEcl();
            latitudeDeg = nearest.GetLatitudeFromCce(cce);
            longitudeDeg = nearest.GetLongitudeFromCce(cce);
            bodyName = nearest.Id ?? string.Empty;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Where a ray from <paramref name="eye"/> first goes under the ground of any body, and which body.
    /// </summary>
    public static bool TryGroundAlong(double3 eye, double3 direction, out double3 groundEcl, out Celestial? nearest)
    {
        groundEcl = default;
        nearest = null;

        try
        {
            if (Universe.CurrentSystem is not { } system) return false;

            double nearestRange = double.MaxValue;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not Celestial body) continue;

                double3 centre = body.GetPositionEcl();
                double top = MaxTerrainHeightMetres(body);

                // Far enough to cross the whole body. Only the part below its highest ground is
                // walked, and the walk stops at the first place it is under it.
                double reach = 2.0 * (Vec.Len(eye - centre) + body.MeanRadius + top);

                // Terrain only. A launch pad is 8 m of pedestal 40 m across, and adding it here
                // models it as an 8 m thicker planet: at 5 km the resolved point moves 2.8 km, and
                // sweeping the cursor over the pad edge swings the bearing from the mount through
                // 168 degrees between one pixel and the next. Where a structure's surface is has
                // no answer in this engine -- see docs/BLOCKED-ON-KSA.md.
                if (!TerrainRay.TryFirstHit(eye, direction, reach, centre, body.MeanRadius, top,
                                            new TerrainHeights(body, accurate: true), out double range)
                    || range >= nearestRange)
                {
                    continue;
                }

                nearest = body;
                nearestRange = range;
            }

            if (nearest is null) return false;

            groundEcl = eye + (Vec.Unit(direction) * nearestRange);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets a craft down at a latitude and longitude on the body it is nearest.
    ///
    /// <para><c>Vehicle.TeleportToLocation</c> does the work, and doing it this way rather than by
    /// writing a position is what makes the craft arrive upright and resting: it builds the
    /// kinematic state from the craft's own bounding box, so the hull ends up on the ground rather
    /// than the origin.</para>
    /// </summary>
    public static bool TryPlaceOnSurface(Vehicle craft, string bodyName,
                                         double latitudeDeg, double longitudeDeg)
    {
        if (!IsAlive(craft)) return false;
        if (!double.IsFinite(latitudeDeg) || !double.IsFinite(longitudeDeg)) return false;

        try
        {
            if (Universe.CurrentSystem is not { } system) return false;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not Celestial body) continue;
                if (body.Id != bodyName) continue;

                craft.TeleportToLocation(body, latitudeDeg, longitudeDeg);
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            Log.Warn($"could not place {DisplayName(craft)}: {e.Message}");
            return false;
        }
    }

    private static ScreenshotCapture? _shots;
    private static bool _lookedForShots;
    private static bool _warnedAboutShots;

    /// <summary>
    /// Asks the game to save a screenshot of its own framebuffer.
    ///
    /// <para>The alternative is <c>tools/screenshot.sh</c>, which grabs the whole primary display
    /// and so refuses unless the game is in front — which it is not, during an unattended run on a
    /// machine somebody is using. This needs no focus, cannot photograph somebody's desktop, and
    /// writes a clean frame: <c>ui</c> and <c>hud</c> are opt-in, so the default is the scene with
    /// no panel over it, which is what anybody judging an effect wants.</para>
    ///
    /// <para>Files land in <c>Documents/exports/screenshots/ksa_&lt;stamp&gt;_&lt;w&gt;x&lt;h&gt;.png</c>.</para>
    ///
    /// <para><b>One private field is the whole obstacle</b>: <c>ScreenshotCapture.Request</c> is public and
    /// <c>Program._screenshotCapture</c> is not exposed. The game's own <c>screenshot</c> terminal
    /// command reaches it the same way. Reflected once, and a KSA rename turns this off rather than
    /// breaking anything — nothing but a picture is lost.</para>
    /// </summary>
    public static bool TryRequestScreenshot(int scale = 1, string flags = "")
    {
        try
        {
            if (!_lookedForShots)
            {
                _lookedForShots = true;
                _shots = typeof(Program)
                         .GetField("_screenshotCapture", BindingFlags.NonPublic | BindingFlags.Instance)
                         ?.GetValue(Program.Instance) as ScreenshotCapture;

                if (_shots is null && !_warnedAboutShots)
                {
                    _warnedAboutShots = true;
                    Log.Warn("screenshots: Program._screenshotCapture did not resolve; "
                             + "scripted captures will write nothing");
                }
            }

            if (_shots is null) return false;

            // A capture already running is dropped by the engine with its own warning, so asking
            // again while one is in flight costs nothing and loses only that frame.
            _shots.Request(scale, flags);
            return true;
        }
        catch (Exception e)
        {
            if (!_warnedAboutShots)
            {
                _warnedAboutShots = true;
                Log.Warn($"screenshots: could not ask for one: {e.Message}");
            }
            return false;
        }
    }




    /// <summary>
    /// Where the star is, for anything that has to know which way the light comes from.
    ///
    /// <para>The system's <c>StellarBody</c> rather than a name: <c>Celestial.Class</c> already
    /// separates a planet from a moon by asking whether its parent is one, so the type is the
    /// system's own answer to "which of these is the sun".</para>
    /// </summary>
    public static bool TryStarPositionEcl(out double3 positionEcl)
    {
        positionEcl = default;

        try
        {
            if (Universe.CurrentSystem is not { } system) return false;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not StellarBody star) continue;

                positionEcl = star.GetPositionEcl();

                return Vec.IsFinite(positionEcl);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The bodies of the current system, by the Id <see cref="TryPlaceOnSurface"/> matches on.
    ///
    /// <para>For putting a craft somewhere the cursor cannot reach. <c>CraftMover</c> resolves its
    /// target off the pointer, so it can only ever set a craft down on the body already being
    /// looked at — which leaves anything airless untestable when no save has a craft there, and
    /// none does.</para>
    ///
    /// <para>Whether a body has an atmosphere comes back beside the name, because a test often
    /// wants to know.</para>
    /// </summary>
    public static void SystemBodies(List<(string Id, bool HasAir)> into)
    {
        into.Clear();

        try
        {
            if (Universe.CurrentSystem is not { } system) return;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not Celestial body) continue;
                if (body.Id is not { Length: > 0 } id) continue;

                into.Add((id, HasAtmosphere(body)));
            }
        }
        catch
        {
            into.Clear();
        }
    }

    /// <summary>
    /// How large something at <paramref name="atEcl"/> appears on screen, in pixels.
    ///
    /// <para>Measured by projecting a point one radius to the camera's right rather than by
    /// reconstructing the field of view: the projection already knows the lens, and asking it
    /// twice cannot disagree with itself.</para>
    /// </summary>
    public static bool TryApparentRadiusPixels(double3 atEcl, double metres, out float pixels)
    {
        pixels = 0f;
        if (!double.IsFinite(metres) || metres <= 0.0) return false;

        try
        {
            if (Program.GetMainCamera() is not { } camera) return false;

            double3 right = camera.GetRightEcl();
            if (!Vec.IsFinite(right) || Vec.Len(right) < 0.5) return false;

            if (!TryProjectAhead(atEcl, out float2 centre)) return false;
            if (!TryProjectAhead(atEcl + Vec.Unit(right) * metres, out float2 edge)) return false;

            float dx = edge.X - centre.X, dy = edge.Y - centre.Y;
            pixels = MathF.Sqrt(dx * dx + dy * dy);
            return float.IsFinite(pixels);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Where a craft stands: the surface point under it, and its latitude and longitude.
    ///
    /// <para>Placing something at a craft has to use the craft's own position rather than the
    /// ground the cursor ray reaches. A ray through a vehicle's middle carries on and meets the
    /// ground <em>behind</em> it, so aiming at a craft and using the ray puts the answer a
    /// vehicle-height's worth of parallax past it.</para>
    /// </summary>
    public static bool TryCraftSurfacePoint(Vehicle craft, out double3 groundEcl,
                                            out double latitudeDeg, out double longitudeDeg,
                                            out string bodyName)
    {
        groundEcl = default;
        latitudeDeg = 0.0;
        longitudeDeg = 0.0;
        bodyName = string.Empty;

        if (!IsAlive(craft)) return false;

        try
        {
            if (craft.Parent is not Celestial body) return false;

            double3 cce = PositionEcl(craft) - body.GetPositionEcl();
            if (!Vec.IsFinite(cce) || Vec.Len(cce) < 1.0) return false;

            groundEcl = body.GetSurfacePositionEclFromCce(cce);
            latitudeDeg = body.GetLatitudeFromCce(cce);
            longitudeDeg = body.GetLongitudeFromCce(cce);
            bodyName = body.Id ?? string.Empty;
            return Vec.IsFinite(groundEcl);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Rough size of a vehicle, used to scale contact and picking checks.</summary>
    public static double MeanRadius(Vehicle v)
    {
        double r = v.MeanRadius;
        return double.IsFinite(r) && r > 0.0 ? r : 5.0;
    }

    public static string DisplayName(Vehicle v)
    {
        try { return string.IsNullOrEmpty(v.Id) ? "unnamed" : v.Id; }
        catch { return "unnamed"; }
    }

    /// <summary>
    /// How fast the ground under a point is moving, in the ecliptic frame: the parent body's own
    /// motion plus its spin at that radius.
    ///
    /// <para>This is the frame a body in flight flies in — its airspeed, what its drag acts
    /// against, and what it points along. It equals the releasing craft's velocity only while that
    /// craft sits still on the ground; something released from a craft <em>moving</em> is what
    /// separates them, and measured against the craft it would have no airspeed at release.</para>
    /// </summary>
    public static double3 GroundVelocityAt(Vehicle platform, double3 positionEcl)
    {
        try
        {
            if (platform.Parent is not Celestial body) return SafeVelocityEcl(platform);

            double3 ground = GroundVelocityAt(body, positionEcl);

            return Vec.IsFinite(ground) ? ground : SafeVelocityEcl(platform);
        }
        catch
        {
            // The craft's own velocity, which is exactly right for a craft standing on the
            // ground and the closest available answer for any other.
            return SafeVelocityEcl(platform);
        }
    }

    /// <summary>
    /// The same ground frame, asked of the body directly.
    ///
    /// <para>Which is what it always was: the platform above is consulted only to reach its parent
    /// and as a fallback answer. A body outliving the craft it left keeps flying in this frame,
    /// and has to ask for it without one.</para>
    /// </summary>
    public static double3 GroundVelocityAt(Celestial body, double3 positionEcl)
    {
        try
        {
            // Cce, not the Cci that GetBodyRates answers with: the separation below is a Cce
            // vector, and the two frames differ by the body's axial tilt -- 23.4 degrees on Earth,
            // which at 465 m/s of surface speed invents up to 190 m/s of velocity out of nothing.
            double3 spin = ((IParentBody)body).GetAngularVelocityCce();
            double3 fromCentre = positionEcl - body.GetPositionEcl();

            // The body's own motion without the spin term, which is the honest answer when the
            // spin cannot be read: wrong by at most the surface speed, where guessing zero would
            // be wrong by the whole orbital velocity.
            if (!Vec.IsFinite(spin) || !Vec.IsFinite(fromCentre)) return body.GetVelocityEcl();

            return body.GetVelocityEcl() + Vec.Cross(spin, fromCentre);
        }
        catch
        {
            return Vec.Zero;
        }
    }

    // Inside another method's catch block, so it may not throw itself. VelocityEcl is a bare
    // property read with no guard of its own, and the case this exists for is a platform that has
    // just been disposed -- which is exactly when it throws.
    private static double3 SafeVelocityEcl(Vehicle platform)
    {
        try
        {
            return VelocityEcl(platform);
        }
        catch
        {
            return Vec.Zero;
        }
    }

    /// <summary>
    /// Gravitational acceleration at <paramref name="positionEcl"/> from the platform's parent body,
    /// in Ecl. Returns zero if the parent or its gravity parameter is unavailable.
    /// </summary>
    public static double3 GravityAt(Vehicle platform, double3 positionEcl)
    {
        try
        {
            return platform.Parent is Celestial body ? GravityAt(body, positionEcl) : Vec.Zero;
        }
        catch
        {
            return Vec.Zero;
        }
    }

    /// <summary>The same pull, asked of the body directly — for something in flight with no craft left.</summary>
    public static double3 GravityAt(Celestial body, double3 positionEcl,
                                   double3 bodyOffsetEcl = default)
    {
        try
        {
            double mu = ((IParentBody)body).Mu;
            if (mu <= 0.0) return Vec.Zero;

            double3 toBody = (body.GetPositionEcl() + bodyOffsetEcl) - positionEcl;
            double dist2 = Vec.Len2(toBody);
            if (dist2 < 1.0) return Vec.Zero;

            return Vec.Unit(toBody) * (mu / dist2);
        }
        catch
        {
            return Vec.Zero;
        }
    }

    /// <summary>
    /// Density of whatever a body is flying through, as a multiple of
    /// <see cref="Medium.ReferenceDensityKgPerM3"/> — Earth's sea-level air, whatever body this is.
    ///
    /// <para>1.0 at Earth's sea level, 0.0 in vacuum and above the atmosphere, and roughly 840 below
    /// Earth's waterline. One reference for every body, because drag belongs to the object and the
    /// air: divided by each body's own sea level, a ball would be dragged through thin air as hard
    /// as through Earth's. One scale covers air and water.</para>
    ///
    /// <para><b>A body with no atmosphere reads 0.0, and that is an answer rather than a
    /// failure.</b> Every airless body in the game hands back no reference, so reading that as the
    /// fallback would put Earth's sea-level air on the Moon.</para>
    ///
    /// <para>The fallback when the atmosphere genuinely <em>cannot be read</em> — a throw, or a
    /// craft with no body under it — is still 1.0: an object that keeps its tuned drag is a far less
    /// confusing failure than one that silently loses all of it and flies several times further.
    /// The two are distinguished because they are different things, and only one of them is a
    /// fault.</para>
    /// </summary>
    public static double MediumDensityRatioAt(Vehicle platform, double3 positionEcl)
    {
        try
        {
            return platform.Parent is Celestial body ? MediumDensityRatioAt(body, positionEcl) : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    /// <summary>The same medium, asked of the body directly — for something in flight with no craft left.</summary>
    public static double MediumDensityRatioAt(Celestial body, double3 positionEcl)
        => MediumDensityRatioAt(body, positionEcl, withOcean: true);

    /// <summary>
    /// Whether a body has an atmosphere at all.
    ///
    /// <para>Asked where a density at a point is the wrong question: what is wanted is whether the
    /// <em>body</em> has air for a buoyant cloud to rise through, which no altitude over one this
    /// answers false for can supply.</para>
    /// </summary>
    public static bool HasAtmosphere(Celestial body)
    {
        try
        {
            return body.GetAtmosphereReference()?.Physical is { } air && air.Height > 0.0;
        }
        catch
        {
            // Unreadable reads as having air: that is the common case and the safer guess.
            return true;
        }
    }

    private static double MediumDensityRatioAt(Celestial body, double3 positionEcl, bool withOcean)
    {
        try
        {
            // Altitude above the mean surface, the same measure KSA's own physics uses. Asked
            // before the air is, because whether a point is under water is its own question: a
            // body can have an ocean and no atmosphere, and resolving the air first returned
            // vacuum for a point at the bottom of one.
            double altitude = Vec.Len(positionEcl - body.GetPositionEcl()) - body.MeanRadius;

            // Below the waterline the medium is the ocean, which is ~840x sea-level air. The
            // ratio is therefore not bounded above by 1.
            // The ocean's IsValid() tests its level (0 m) and transparency depth (100 m) against
            // an astronomical 100 km bar, so it is false wherever there is water. A body with no
            // ocean hands back null, which is the discriminator that means it.
            OceanReference? ocean = body.GetOceanReference();
            if (withOcean && ocean is { } sea && sea.Density > 0.0 && altitude < sea.Level)
            {
                double water = sea.Density / Medium.ReferenceDensityKgPerM3;
                return double.IsFinite(water) && water > 0.0 ? water : 1.0;
            }

            // Never gate on KSA's own IsValid(). DistanceReference.IsValid requires a distance
            // over 100 km — an astronomical-scale sanity check — and the atmosphere's applies it
            // to the scale height, 8 km on Earth. So air.IsValid() is false for every realistic
            // atmosphere, and trusting it reports vacuum at ground level. Check the terms this
            // actually divides by instead.
            //
            // No reference, or one with nothing physical in it, is the model saying there is no
            // air — which is KNOWLEDGE, not a failed read, and every airless body in the game
            // answers this way. Read as the reference density it would put Earth's sea-level air on
            // the Moon, and everything flying there would be dragged by air that is not there.
            AtmosphereReference? atmosphere = body.GetAtmosphereReference();
            if (atmosphere?.Physical is not { } air) return 0.0;

            double seaLevel = air.SeaLevelDensity;
            double scaleHeight = air.ScaleHeight.InMeters();
            if (!(seaLevel > 0.0) || !(scaleHeight > 0.0)) return 0.0;

            if (altitude < 0.0) altitude = 0.0;
            if (altitude >= air.Height) return 0.0;

            double ratio = air.GetAtmosphericDensityAtAltitude(altitude) / Medium.ReferenceDensityKgPerM3;
            return double.IsFinite(ratio) && ratio >= 0.0 ? ratio : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    /// <summary>
    /// Blocks until KSA's vehicle solver jobs have finished the step they are working on.
    ///
    /// <para><b>Required before destroying a vehicle from a mod hook.</b> Disposing a vehicle
    /// removes it from its <c>PhysicsBubble</c>'s <c>_vehicleStates</c>, which is the list
    /// <c>VehicleUpdateTask.Run</c> enumerates off the main thread — the dispose surfaces as
    /// <c>InvalidOperationException: Collection was modified</c> inside the engine.</para>
    ///
    /// <para>Waiting on the scheduler alone is the whole barrier, even though the work fans out
    /// across a second job system: <c>Run</c> steps the bubbles through
    /// <c>JobSystems.VehicleWorkerPool</c> inside a <c>using ParallelBatch</c>, and disposing
    /// that batch awaits it — so the pool cannot still be running once the scheduler is idle.</para>
    ///
    /// <para>No mod hook sits in the safe window. <c>PrepareFrame</c> takes this barrier at
    /// <c>Program.cs:1966</c> and re-dispatches at <c>:2003</c>, while the GUI hook fires at
    /// <c>:2051</c> and the frame hook later still — so moving the call between hooks cannot
    /// help. Taking the barrier costs a stall only on frames where something dies, and those jobs
    /// had to finish before the next <c>PrepareFrame</c> anyway.</para>
    /// </summary>
    public static void WaitForVehicleSolvers()
    {
        try
        {
            JobSystems.VehicleSolver?.Wait();
        }
        catch (Exception e)
        {
            // Falling through to the destroy leaves the race in place; taking the frame down is
            // worse.
            Log.Warn($"could not join the vehicle solvers before a destroy ({e.GetType().Name})");
        }
    }

    /// <summary>
    /// Takes a vehicle out of the world without breaking it up. Same threading rule as
    /// <see cref="Destroy"/>, and it takes that barrier itself: removing a vehicle also mutates the
    /// shapes registry, which the vehicle worker holds for its whole run, and a removal refused
    /// half-way left the physics bubble indexing a vehicle list one shorter than it thought.
    ///
    /// <para><c>DestroyVehicleFromEvent</c> runs the engine's failure machinery, which ends in
    /// <c>PartFailure.ShedDebris(vehicle, 12)</c> — so destroying one spent stage can leave up to
    /// twelve vehicles behind rather than none. For disposal that is the opposite of what was
    /// asked, and the debris keeps the physics bubble from returning to rails.</para>
    ///
    /// <para><c>EndMission</c> rather than <c>Kill</c>: nothing aboard a spent stage died, and the
    /// crew disposition is the only thing separating the two.</para>
    /// </summary>
    public static void Remove(Vehicle v)
    {
        if (!IsAlive(v)) return;
        try
        {
            WaitForVehicleSolvers();
            using ShapesUnlock shapes = ConstraintSim.UnlockShapesBlocking();

            Universe.DestroyVehicle(v, CrewDisposition.EndMission);
            InvalidateCensus();
        }
        catch (Exception e)
        {
            Log.Warn($"could not remove {DisplayName(v)} from the world: {e.Message}");
        }
    }

    /// <summary>
    /// Destroys a vehicle, attributing it to collision damage. Must be called from the main
    /// thread, and only after <see cref="WaitForVehicleSolvers"/> — see there for why.
    ///
    /// <para>This breaks the vehicle up and leaves debris. To take one out of the world instead,
    /// use <see cref="Remove"/>.</para>
    /// </summary>
    public static void Destroy(Vehicle v, float severity)
    {
        if (!IsAlive(v)) return;
        try
        {
            var evt = new VehicleDestructionEvent
            {
                Cause = VehicleDestructionCause.Collision,
                PeakGLoad = severity,
                PeakDynamicPressure = severity,
            };
            Universe.DestroyVehicleFromEvent(v, evt);

            // The census holds a reference, and a disposed vehicle is not what a fresh walk would
            // have returned.
            InvalidateCensus();
        }
        catch (Exception e)
        {
            Log.Warn($"failed to destroy {DisplayName(v)}: {e.Message}");
        }
    }

    // ---- Part damage ----------------------------------------------------


    // ---- Rendering ------------------------------------------------------

    // The overlay is drawn relative to an anchor vehicle rather than by converting absolute
    // positions. See BeginDraw for why.
    private static DrawAnchor _anchor;
    private static bool _anchored;

    /// <summary>
    /// Establishes the frame for this draw pass. Call once before any Draw*Ecl call.
    ///
    /// <para>Naive conversion — <c>camera.EclToEgo(v.GetPositionEcl())</c> — puts the overlay in
    /// the wrong place. <see cref="Vehicle.GetPositionEcl"/> returns a value computed in
    /// <c>UpdatePerFrameData</c> from <c>Orbit.StateVectors</c>, i.e. the analytic on-rails
    /// position. A landed craft is held where physics puts it, not where its degenerate Kepler
    /// orbit says, and the two differ by enough to be obvious on screen.</para>
    ///
    /// <para>KSA renders vehicles via <c>camera.GetPositionEgo(vehicle)</c>, which returns
    /// <c>-PositionCce</c> for the followed craft and uses <c>KinematicStates.PositionPhys</c>
    /// for others in the same bubble — the physics position in both cases. Anchoring to that and
    /// adding Ecl offsets (exact, since Ego is a pure translation of Ecl) puts the overlay
    /// exactly where the game draws the craft.</para>
    /// </summary>
    /// <param name="anchorEcl">
    /// The anchor's Ecl position **captured at the same instant as everything else being
    /// drawn** — not re-read here. Ecliptic positions near Earth sweep past at ~29.8 km/s, so
    /// a reference taken one frame later than the geometry it is differenced against is about
    /// 500 m stale at 60 fps, and the whole overlay lands that far from the craft.
    /// </param>
    public static bool BeginDraw(Vehicle anchor, double3 anchorEcl)
    {
        _anchored = false;
        if (!IsAlive(anchor)) return false;

        try
        {
            // The camera the frame will actually be rendered with, not the main viewport's.
            Camera camera = Program.GetRenderCamera() ?? Program.GetMainCamera();
            if (camera is null) return false;

            // See DrawAnchor for why these are sampled at different instants, and why
            // collapsing them into one puts the whole overlay beside the craft.
            //
            // GetPositionEgo, not EclToEgo. Its branching on what the camera follows is the
            // engine answering correctly per case — exact for the followed craft, physics-based
            // for others in its bubble — and it is the same call KSA renders vehicles with.
            // EclToEgo instead measures against camera.PositionEcl, which only agrees with the
            // rendered scene when the followed craft's analytic and physics positions coincide.
            // That holds for a landed craft and fails once the camera follows something in
            // flight, which is when anything drawn to a vehicle stops lining up.
            _anchor = new DrawAnchor(camera.GetPositionEgo(anchor), anchorEcl);

            if (!_anchor.IsValid) return false;


            _anchored = true;
            return true;
        }
        catch
        {
            return false;
        }
    }


    /// <summary>Converts an Ecl position into the anchored Ego frame.</summary>
    public static bool TryEclToEgo(double3 posEcl, out double3 posEgo)
    {
        if (_anchored)
        {
            posEgo = _anchor.ToEgo(posEcl);
            return true;
        }

        posEgo = Vec.Zero;
        return false;
    }

    // The views this mod may drive, in the order its viewport numbers index. GameViews rather than
    // Views because only a game viewport has a camera and controllers -- which also keeps the
    // part-thumbnail renderer out, since it is not one.
    private static ReadOnlySpan<IGameViewport> GameViewports
    {
        get
        {
            try { return ViewportRegistry.GameViews; }
            catch { return default; }
        }
    }

    /// <summary>
    /// Where a point in Ecl lands on screen inside one viewport, in absolute screen pixels.
    ///
    /// <para>The camera projects into its own framebuffer, so the result is viewport-local and
    /// has to be offset by where that window sits — otherwise an overlay drawn from it lands on
    /// the main view instead.</para>
    ///
    /// <para>False when the point is behind the camera or outside the window, which is the
    /// caller's cue to draw nothing rather than to clamp it to an edge.</para>
    /// </summary>
    public static bool TryProjectIntoViewport(int index, double3 pointEcl, out float2 screen,
                                              out int width, out int height)
    {
        screen = default;
        width = height = 0;
        try
        {
            ReadOnlySpan<IGameViewport> viewports = GameViewports;
            if (index < 0 || index >= viewports.Length) return false;

            IGameViewport viewport = viewports[index];
            Camera camera = viewport.Mode == CameraMode.Fixed ? viewport.BaseCamera
                                                              : viewport.GetCamera();
            if (camera is null) return false;

            width = viewport.Width;
            height = viewport.Height;
            if (width <= 0 || height <= 0) return false;

            float2 local = camera.EclToScreen(pointEcl, ignoreBehind: false);
            if (!float.IsFinite(local.X) || !float.IsFinite(local.Y)) return false;
            if (local.X < 0f || local.Y < 0f || local.X > width || local.Y > height) return false;

            screen = new float2(viewport.Position.X + local.X, viewport.Position.Y + local.Y);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // The engine's default field of view, and the widest and narrowest this mod will set: the
    // projection throws for a field of zero or more than half a turn.
    private const double DefaultFovDeg = 50.0;
    private const double MinFovDeg = 1.0;
    private const double MaxFovDeg = 120.0;

    /// <summary>The main view's own field of view (deg), or the engine's default if unreadable.</summary>
    public static double MainViewFovDeg()
    {
        try
        {
            if (Program.MainViewport?.GetCamera() is not { } camera) return DefaultFovDeg;

            double degrees = double.RadiansToDegrees(camera.GetFieldOfView());

            return double.IsFinite(degrees) && degrees > 0.0 && degrees < 180.0
                ? degrees
                : DefaultFovDeg;
        }
        catch
        {
            return DefaultFovDeg;
        }
    }

    /// <summary>
    /// Narrows or widens the main view.
    ///
    /// <para>Clamped here and not merely by the caller: <c>SetFieldOfView</c> does not clamp, and
    /// <c>UpdateProjection</c> throws <c>ArgumentOutOfRangeException</c> for a field of zero or
    /// more than half a turn — out of the frame hook, which takes the mod down with it.</para>
    ///
    /// <para>Has to be rewritten every frame it is wanted. The player's zoom keys route through
    /// <c>ChangeFieldOfView</c>, which clamps to 15°–120°, so a single keypress throws away
    /// anything narrower than 15° and there is no notification that it happened.</para>
    /// </summary>
    public static bool TrySetMainViewFov(double degrees)
    {
        if (!double.IsFinite(degrees)) return false;

        try
        {
            if (Program.MainViewport?.GetCamera() is not { } camera) return false;

            float wanted = (float)Math.Clamp(degrees, MinFovDeg, MaxFovDeg);
            if (Math.Abs(double.RadiansToDegrees(camera.GetFieldOfView()) - wanted) < 1e-3) return true;

            camera.SetFieldOfView(wanted);
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"could not set the field of view: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reports how the cursor is being turned into a ray, and how far the answer lands from the
    /// pointer once projected back. The round trip is the measurement that matters: it is the
    /// error actually on screen, whatever the intermediate conventions turn out to be.
    /// </summary>
    public static string DescribeCursorRay(double3 solvedEcl)
    {
        try
        {
            float2 cursor = ImGui.GetMousePos();
            ImGuiViewportPtr main = ImGui.GetMainViewport();

            string chosen = "none";
            ReadOnlySpan<IGameViewport> viewports = GameViewports;
            for (int i = 0; i < viewports.Length; i++)
            {
                IGameViewport v = viewports[i];
                if (!v.Visible) continue;
                if (!CursorAim.TryToViewport(cursor, v.Position, v.Width, v.Height, out float2 local))
                {
                    continue;
                }

                Camera? c = v.GetCamera();
                chosen = $"vp{i} pos={v.Position.X:F0},{v.Position.Y:F0} "
                         + $"size={v.Width}x{v.Height} fb={c?.FramebufferSize.X}x{c?.FramebufferSize.Y} "
                         + $"local={local.X:F0},{local.Y:F0}";
                break;
            }

            string back = TryProjectAhead(solvedEcl, out float2 screen)
                              ? $"back={screen.X:F0},{screen.Y:F0} "
                                + $"err={screen.X - cursor.X:F0},{screen.Y - cursor.Y:F0}"
                              : "back=offscreen";

            return $"cursor={cursor.X:F0},{cursor.Y:F0} "
                   + $"mainvp={main.Pos.X:F0},{main.Pos.Y:F0} {main.Size.X:F0}x{main.Size.Y:F0} "
                   + $"{chosen} {back}";
        }
        catch (Exception e)
        {
            return $"(describe failed: {e.Message})";
        }
    }

    public static bool TryCursorRayEcl(out double3 originEcl, out double3 directionEcl)
    {
        originEcl = default;
        directionEcl = default;
        try
        {
            float2 cursor = ImGui.GetMousePos();

            ReadOnlySpan<IGameViewport> viewports = GameViewports;
            for (int i = 0; i < viewports.Length; i++)
            {
                IGameViewport v = viewports[i];
                if (!v.Visible) continue;

                if (v.GetCamera() is not { } camera) continue;

                // Framebuffer pixels, not viewport pixels: ScreenToEgoRay divides by the camera's
                // own framebuffer, and a render or display scale makes those different sizes.
                if (!CursorAim.TryToFramebuffer(cursor, v.Position, v.Width, v.Height,
                                                camera.FramebufferSize.X, camera.FramebufferSize.Y,
                                                out float2 local))
                {
                    continue;
                }

                double3 direction = camera.ScreenToEgoRay(local).Direction;
                if (!CursorAim.IsUsableDirection(direction)) continue;

                originEcl = camera.EgoToEcl(Vec.Zero);
                directionEcl = Vec.Unit(direction);
                return Vec.IsFinite(originEcl);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Drops what belongs to the previous frame. Called once where the simulation is stepped.
    /// </summary>
    public static void BeginFrame()
    {
        // The draw anchor, which every Draw*Ecl call converts through. Left standing from an
        // earlier frame it is the platform's position then, not now, and the overlay lands beside
        // the craft by whatever the world has moved since: about 500 m per frame near Earth, and
        // unbounded once a frame passes without any pass establishing one. Cleared here so a
        // caller that forgets BeginDraw draws nothing, which is visible, rather than drawing in
        // the wrong place, which is not.
        _anchored = false;
    }


    // Whether a ray could ever hit this craft. Mirrors what RayCastEgo actually tests -- one level
    // of SubParts, each needing a MeshViewModule -- so the answer cannot disagree with the cast.
    internal static bool HasPickableMesh(ReadOnlySpan<Part> parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            ReadOnlySpan<Part> subs = parts[i].SubParts;

            for (int j = 0; j < subs.Length; j++)
            {
                if (subs[j].Modules.HasAny<MeshViewModule>()) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Projects a world point onto the main viewport, culling anything behind the camera.
    ///
    /// <para>Distinct from <see cref="TryProjectIntoViewport"/>, which passes
    /// <c>ignoreBehind: false</c>. That is right for a view pointed at its own target, which
    /// cannot be looking away from it, and wrong for a marker over an arbitrary
    /// craft: <c>EgoToScreen</c> only tests the point against the camera's forward when asked, so
    /// without it a site *behind* the camera draws a bracket in front of it.</para>
    /// </summary>
    public static bool TryProjectAhead(double3 pointEcl, out float2 screen)
    {
        screen = default;
        try
        {
            if (Program.MainViewport is not { } viewport) return false;
            if (viewport.GetCamera() is not { } camera) return false;
            if (viewport.Width <= 0 || viewport.Height <= 0) return false;

            float2 local = camera.EclToScreen(pointEcl, ignoreBehind: true);
            if (!float.IsFinite(local.X) || !float.IsFinite(local.Y)) return false;
            if (local.X < 0f || local.Y < 0f || local.X > viewport.Width || local.Y > viewport.Height)
            {
                return false;
            }

            screen = new float2(viewport.Position.X + local.X, viewport.Position.Y + local.Y);
            return true;
        }
        catch
        {
            return false;
        }
    }



    /// <summary>
    /// How far back the orbit camera sits, as the power KSA stores it in.
    ///
    /// <para>A power rather than a distance: the engine scales it by the followed object's mean
    /// radius, so one number frames a ground vehicle and a rocket alike. Written straight onto the
    /// controller, which is what <see cref="GoTo"/> does when it carries a craft's own saved
    /// zoom across.</para>
    /// </summary>
    public static bool SetOrbitZoomPower(double power)
    {
        try
        {
            if (!double.IsFinite(power)) return false;

            Program.MainViewport.OrbitController.DistancePower = power;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Points the camera at a craft and takes control of it.
    ///
    /// <para>Three steps: follow, control, match the zoom. Doing fewer leaves the camera watching
    /// one craft while the controls drive another, or the view snapped to a zoom that belonged to
    /// the last vehicle.</para>
    ///
    /// <para><b>It deliberately does not rebuild the vehicle's derived data.</b> Nothing here
    /// modifies a part tree, and the engine does not do it either when it switches which vehicle is
    /// followed and controlled — <c>Camera.SetFollow</c> sets <c>ControlledVehicle</c> and stops.
    /// The rebuild reaches the shapes registry, which the vehicle worker holds for its whole run, and
    /// this mod's hooks land inside that run whenever the worker has not finished; it threw from here
    /// on handovers after a decoupler split. Anything that has to build a vehicle waits the run
    /// out instead.</para>
    /// </summary>
    /// <returns>False if the craft is gone, or the engine refused any part of it.</returns>
    public static bool GoTo(Vehicle? vehicle)
    {
        if (!IsAlive(vehicle)) return false;

        try
        {
            Camera? camera = Program.GetMainCamera();
            if (camera is null) return false;

            camera.SetFollow(vehicle!, tidalLocking: true);
            Program.ControlledVehicle = vehicle;
            Program.MainViewport.OrbitController.DistancePower = vehicle!.OrbitView.DistancePower;
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"could not go to {DisplayName(vehicle!)}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Whether a craft has anything for a surface-attached part to ride on.
    ///
    /// <para>Cheap, for callers that walk every craft every frame: the first part usually answers.
    /// See <see cref="PartRides"/> for the rule.</para>
    /// </summary>
    public static bool HasPlatform(Vehicle? vehicle)
    {
        if (!IsAlive(vehicle)) return false;

        try
        {
            ReadOnlySpan<Part> parts = vehicle!.Parts.Parts;

            // An unreadable or empty tree is a platform: a craft the mod stops recognising is a
            // worse answer than one it recognises for a frame while the tree is rebuilt.
            if (parts.Length == 0) return true;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!PartRides(parts[i])) return true;
            }

            return false;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Whether a part attaches to the side of something rather than stacking on it, and so cannot
    /// be a craft on its own.
    ///
    /// <para>Asked of the game rather than recorded here. <c>VehicleEditor.IsAllowedAsRootPart</c>
    /// rejects a part if any of its connectors carries <c>ToSurface</c> or <c>FromSurface</c>, and
    /// that is the same question: a craft rooted on a surface-attached part is a craft the editor
    /// would never have let anybody build. It happens anyway, because
    /// isolating a failed part splits the vehicle wherever it can.</para>
    ///
    /// <para>A part with no connectors at all rides nothing and is treated as a platform, which is
    /// the reading that cannot make a working craft disappear.</para>
    /// </summary>
    public static bool PartRides(Part part)
    {
        try
        {
            List<Part.Connector.TemplateBase> connectors = part.Template.Connectors;

            for (int i = 0; i < connectors.Count; i++)
            {
                Part.Connector.Flag flags = connectors[i].Flags;

                if ((flags & (Part.Connector.Flag.ToSurface | Part.Connector.Flag.FromSurface)) != 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // Unreadable is a platform, for the same reason as no connectors: the cost of guessing
            // wrong the other way is a craft the mod stops recognising.
            return false;
        }
    }

    /// <summary>Moves the main orbit camera to a pair of angles.</summary>
    public static bool TryWriteMainOrbit(double azimuth, double elevation)
    {
        if (!double.IsFinite(azimuth) || !double.IsFinite(elevation)) return false;

        try
        {
            if (Program.GetMainCamera()?.Following?.OrbitView is not { } view) return false;

            view.Azimuth = azimuth;

            // The game clamps it on every path that writes it, so a value past the pole would be
            // one this camera can never report back and the write would look refused forever.
            view.Elevation = Math.Clamp(elevation, -Math.PI / 2.0, Math.PI / 2.0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the main view is currently following this craft.
    ///
    /// <para>Asked before anything borrows the view: a craft on the far side of the world taking
    /// the camera off whatever the player is watching is a hijack, however good the shot.</para>
    /// </summary>
    public static bool MainViewFollows(IFollowable? target)
    {
        if (target is null) return false;

        try
        {
            return ReferenceEquals(Program.MainViewport?.GetCamera()?.Following, target);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>What the main view was doing before something borrowed it.</summary>
    /// <param name="FovDeg">
    /// The field of view it was set to. Carried because a borrower may zoom, and one that
    /// hands back everything except the zoom leaves the player at 3° with no control that reaches
    /// it — their own zoom keys clamp at 15° and cannot widen past it.
    /// </param>
    public readonly record struct MainView(IFollowable? Following, CameraMode Mode, double FovDeg,
                                           bool Valid);

    /// <summary>
    /// Records the main view so it can be handed back.
    ///
    /// <para>Taken before the first write, not after. Setting Fixed clears the follow, so a
    /// reading taken afterwards describes the borrowed state and restoring from it leaves the
    /// player at a fixed point in space with no way home.</para>
    /// </summary>
    public static MainView RememberMainView()
    {
        try
        {
            if (Program.MainViewport is not { } viewport) return default;

            return new MainView(viewport.GetCamera()?.Following, viewport.Mode, MainViewFovDeg(), true);
        }
        catch (Exception e)
        {
            Log.Warn($"could not read the main view: {e.Message}");
            return default;
        }
    }

    /// <summary>
    /// Whether something the view was following is still there to go back to.
    ///
    /// <para>A destroyed craft is not, and it is the ordinary case rather than a corner: the thing
    /// a borrower was watching is often the thing that just blew up. The engine has already
    /// answered the same question for itself by pointing the view at the wreckage, so the right
    /// move is to leave that alone — restoring over it puts the camera on a disposed vehicle and
    /// holds it there.</para>
    /// </summary>
    public static bool CanFollow(IFollowable? target) => target switch
    {
        null => false,
        Vehicle v => !v.IsDisposed,
        _ => true,
    };

    /// <summary>
    /// Puts back whatever the view was following before a mod borrowed it.
    ///
    /// <para>Separate from the mode: following something of the mod's own has to be undone even
    /// when the mode never changed, and leaving a camera pointed at an object the mod is about to
    /// forget is how a view ends up stuck on something that no longer exists.</para>
    /// </summary>
    public static bool RestoreFollow(MainView saved)
    {
        if (!saved.Valid || saved.Following is null) return false;

        try
        {
            if (Program.MainViewport?.GetCamera() is not { } camera) return false;

            camera.SetFollow(saved.Following, tidalLocking: true, changeControl: false, alert: false);
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"could not restore what the view was following: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Hands the main view back in the two halves it was taken in, and says whether each half
    /// arrived. Every borrower goes through here, so a refusal cannot mean two different things in
    /// two files.
    ///
    /// <para>Order is the contract: what the view follows is read before anything is written,
    /// because the restore is about to change it and "did the player move this themselves" has no
    /// answer afterwards.</para>
    ///
    /// <para>A follow that cannot be given back is <b>not</b> a refusal. A destroyed craft is the
    /// ordinary way a chase ends, and a follow the player has taken is theirs to
    /// keep — neither has anything to retry, so counting either would leave a caller retrying
    /// something that is never coming back.</para>
    /// </summary>
    /// <returns>False if either half was refused, and the caller still holds the view.</returns>
    public static bool TryHandBackMainView(MainView saved, IFollowable? followed,
                                           out bool mode, out bool follow)
    {
        bool followIsOurs = MainViewFollows(followed);

        mode = BeginRestoreMainView(saved);
        follow = !followIsOurs || !CanFollow(saved.Following) || RestoreFollow(saved);

        return mode && follow;
    }


    // The engine's own controller, kept so it can be put back. Static because there is one main
    // viewport and the swap outlives any single borrower of it.
    private static FixedController? _stockFixedController;

    // The one name rather than a signature that the camera stands on, and the one thing
    // api-surface.sh cannot check for it. GameViewport.FixedController is an auto-property whose
    // setter is protected, behind IGameViewport's get-only one, so the backing field is the only
    // way in, checked and warned about if it moves.
    //
    // Null here is not a fault: the engine's own controller stays, which costs the levelled
    // horizon and puts a driven view's aim a frame behind again. Both are worse pictures, not crashes.
    private static FieldInfo? _fixedControllerField;
    private static bool _lookedForFixedControllerField;

    private static FieldInfo? FixedControllerField(IGameViewport viewport)
    {
        if (_lookedForFixedControllerField) return _fixedControllerField;
        _lookedForFixedControllerField = true;

        _fixedControllerField = viewport.GetType().GetField(
            "<FixedController>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

        if (_fixedControllerField is null)
        {
            Log.Warn("camera: no FixedController backing field on "
                     + $"{viewport.GetType().Name} - keeping KSA's own controller, so the horizon "
                     + "is not levelled and a driven view's aim lags a frame. If the property has a "
                     + "public setter again, use it and delete this.");
        }

        return _fixedControllerField;
    }

    // Puts LevelHorizonController on the main viewport, once, and answers with whichever
    // controller is now in place. Null only if the viewport has no controller at all.
    //
    // If the swap itself fails the engine's own is returned and everything carries on with the
    // roll it always had -- this is an extension point nobody promised, so it has to be allowed
    // to stop working.
    private static FixedController? LevelTheHorizon(IGameViewport viewport)
    {
        if (viewport.FixedController is LevelHorizonController already) return already;

        try
        {
            if (viewport.BaseCamera is not { } camera) return viewport.FixedController;
            if (FixedControllerField(viewport) is not { } field) return viewport.FixedController;

            var level = new LevelHorizonController(camera);

            _stockFixedController ??= viewport.FixedController;
            field.SetValue(viewport, level);

            // The write is the whole point, and a silent no-op would read exactly like a working
            // swap: everything downstream still finds *a* controller.
            if (!ReferenceEquals(viewport.FixedController, level))
            {
                Log.Warn("camera: the FixedController write did not take - keeping KSA's own");
                _stockFixedController = null;
                return viewport.FixedController;
            }

            Log.Info("camera: levelling the horizon on the main view");

            return level;
        }
        catch (Exception e)
        {
            Log.Warn($"could not level the horizon, using KSA's own controller: {e.Message}");
            return viewport.FixedController;
        }
    }

    /// <summary>Puts KSA's own fixed controller back, if it was ever swapped out.</summary>
    public static void RestoreStockController()
    {
        if (_stockFixedController is not { } stock) return;

        try
        {
            if (Program.MainViewport is { } viewport
                && FixedControllerField(viewport) is { } field)
            {
                field.SetValue(viewport, stock);
            }
            Log.Info("camera: gave the fixed controller back");
        }
        catch (Exception e)
        {
            Log.Warn($"could not restore the fixed controller: {e.Message}");
        }
        finally
        {
            _stockFixedController = null;
        }
    }

    /// <summary>
    /// Points the main view from a place, using Fixed mode as it is meant to be used.
    ///
    /// <para>The camera keeps following whatever it followed. <c>FixedController.OnFrame</c> puts
    /// it at <c>following.GetPositionEcl() + CameraOffset</c> looking along <c>CameraRotation</c>,
    /// so those two fields are the whole interface — and the offset is measured from the followed
    /// craft, not from the world.</para>
    ///
    /// <para><c>CameraRotation</c> must be non-zero before the mode is set. The controller crosses
    /// it with the frame's up and normalises, so a zero vector divides by zero — which is the
    /// entire reason this mode has a reputation for crashing.</para>
    /// </summary>
    /// <param name="offsetFromFollowed">
    /// Where the camera goes <em>relative to the craft the view is following</em>, not an absolute
    /// position. The controller adds it to <c>following.GetPositionEcl()</c> later in the frame,
    /// so an offset derived from that position here is measured against a different instant from
    /// the one it is applied to — which is a frame of the platform's motion, every frame, and
    /// reads as the thing being watched shivering.
    /// </param>
    /// <param name="fovDeg">
    /// The field this borrower wants. Required, and deliberately not optional: a borrower that
    /// says nothing about the field inherits whatever the last one left behind, which can be a
    /// magnified few degrees. Making it part of driving the view is what stops that being possible.
    /// </param>
    /// <param name="pose">
    /// Somewhere the controller may ask again, inside the engine's own viewport pass. Null is the
    /// right answer for anything that does not need to be in phase with the frame, and passing it
    /// every call is what takes the previous borrower's source back off the controller.
    /// </param>
    public static bool TryLookFromMainViewport(double3 offsetFromFollowed, double3 forwardEcl,
                                               double3 upEcl, double fovDeg, IViewPose? pose = null)
    {
        if (!Vec.IsFinite(offsetFromFollowed) || !Vec.IsFinite(forwardEcl)) return false;
        if (Vec.Len2(forwardEcl) < 1e-12) return false;

        try
        {
            if (Program.MainViewport is not { } viewport) return false;
            if (viewport.GetCamera()?.Following is null) return false;

            FixedController? controller = LevelTheHorizon(viewport);
            if (controller is null) return false;

            // Only when the engine is deriving up for itself, which it does whenever the level
            // controller could not be installed. Its axis is then ecliptic +Z, and a view along
            // that divides by zero. LevelHorizonController has no such direction: it falls back
            // for a view along the up it was given rather than refusing, so the chase is never
            // dropped mid-flight.
            if (controller is not LevelHorizonController
                && Math.Abs(Vec.Dot(Vec.Unit(forwardEcl), new double3(0, 0, 1))) > 0.999)
            {
                return false;
            }

            // Set before the mode, every time. A frame drawn in Fixed with a zero rotation is the
            // crash, and setting the mode first leaves exactly that gap.
            controller.CameraRotation = Vec.Unit(forwardEcl);
            controller.CameraOffset = offsetFromFollowed;

            if (controller is LevelHorizonController level)
            {
                level.UpEcl = upEcl;

                // Every call, including with null. A borrower that stops driving must not leave
                // its source installed, or the controller goes on asking a borrower that has let go.
                level.Pose = pose;
            }

            // With the pose and not after it. A borrower supplying a pose source restates the
            // field in phase every frame, so this is the take-over frame's value and the fallback
            // if that source ever refuses.
            TrySetMainViewFov(fovDeg);

            if (viewport.Mode != CameraMode.Fixed) viewport.SetCameraMode(CameraMode.Fixed);

            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"could not drive the main view: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Takes the mod off the controller without touching the mode, the follow or the field.
    ///
    /// <para>Forgets the up that was being supplied and the source being asked. The controller
    /// stays installed for the session — nothing but a mod puts a viewport in Fixed mode, so there
    /// is nothing to disturb — but with no up it behaves exactly as KSA's own does, rather than
    /// holding one from a chase that is over.</para>
    /// </summary>
    public static void StopDrivingMainView()
    {
        try
        {
            if (Program.MainViewport?.FixedController is LevelHorizonController level)
            {
                level.UpEcl = Vec.Zero;
                level.Pose = null;
                level.Forget();
            }
        }
        catch (Exception e)
        {
            Log.Warn($"could not stop driving the main view: {e.Message}");
        }
    }

    public static bool BeginRestoreMainView(MainView saved)
    {
        if (!saved.Valid) return false;

        try
        {
            if (Program.MainViewport is not { } viewport) return false;

            StopDrivingMainView();

            // Before the mode, so a frame drawn during the handover is drawn at the player's own
            // field rather than at the borrower's.
            TrySetMainViewFov(saved.FovDeg);

            if (viewport.Mode != saved.Mode) viewport.SetCameraMode(saved.Mode);
            return true;
        }
        catch (Exception e)
        {
            Log.Warn($"could not restore the main view: {e.Message}");
            return false;
        }
    }



    /// <summary>
    /// Puts a point on the ground beneath it: same direction from the body's centre, radius taken
    /// from the terrain there.
    ///
    /// <para>What makes a ring drawn on a slope follow the slope. A ring at one radius is flat in
    /// space, so on anything but level ground half of it is buried and the other half floats.</para>
    /// </summary>
    public static bool TrySnapToGround(double3 nearEcl, out double3 onGroundEcl) =>
        TrySnapToGround(nearEcl, out onGroundEcl, out _);

    /// <summary>
    /// As above, and hands back the centre of the body it draped onto -- which it had to find
    /// anyway. A caller that needs the local up gets it for free instead of walking the system
    /// again for a body this has already identified.
    /// </summary>
    public static bool TrySnapToGround(double3 nearEcl, out double3 onGroundEcl, out double3 centreEcl)
    {
        centreEcl = Vec.Zero;
        onGroundEcl = nearEcl;

        return NearestCelestial(nearEcl) is { } nearest
               && TrySnapToGround(nearest, nearEcl, out onGroundEcl, out centreEcl);
    }

    // The body whose centre is nearest a point, by a walk of the whole system. Found once for a
    // whole draped ring rather than once a point: every point of a ring is over the same body.
    private static Celestial? NearestCelestial(double3 nearEcl)
    {
        try
        {
            if (Universe.CurrentSystem is not { } system) return null;

            Celestial? nearest = null;
            double best = double.MaxValue;

            for (int i = 0; i < system.Count; i++)
            {
                if (system.GetIndex(i) is not Celestial body) continue;

                double distance = Vec.Len(nearEcl - body.GetPositionEcl());
                if (distance >= best) continue;

                best = distance;
                nearest = body;
            }

            return nearest;
        }
        catch
        {
            return null;
        }
    }

    // The snap onto a body already found.
    private static bool TrySnapToGround(Celestial nearest, double3 nearEcl, out double3 onGroundEcl,
                                        out double3 centreEcl)
    {
        centreEcl = Vec.Zero;
        onGroundEcl = nearEcl;

        try
        {
            double3 centre = nearest.GetPositionEcl();
            centreEcl = centre;
            double3 dirCce = Vec.Unit(nearEcl - centre);
            if (Vec.Len2(dirCce) < 0.5) return false;

            double height = nearest.GetTerrainHeightFromDirCce(dirCce, accurate: true);
            if (!double.IsFinite(height)) return false;

            onGroundEcl = centre + dirCce * (nearest.MeanRadius + height);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// A torus: a ring of solid spheres, draped onto the terrain.
    ///
    /// <para>Spheres because they are the only solid thing the gizmo renderer draws — <c>Render</c>
    /// is <c>RenderSpheres</c> then <c>RenderLines</c>, with no filled polygon anywhere. Enough of
    /// them around the ring, each wider than the gap to the next, and the result reads as a tube
    /// rather than as beads.</para>
    /// </summary>
    /// <param name="tubeRadius">Thickness of the ring itself.</param>
    public static void DrawTorusEcl(double3 centreEcl, double3 normalEcl, double ringRadius,
                                    double tubeRadius, float4 colour, bool drape = true)
    {
        if (Program.GizmosRenderer is null) return;
        if (!Vec.IsFinite(centreEcl) || !(ringRadius > 0.0) || !(tubeRadius > 0.0)) return;

        double3 up = Vec.Unit(normalEcl);
        if (Vec.Len2(up) < 0.5) return;

        double3 seed = Math.Abs(up.X) < 0.9 ? new double3(1, 0, 0) : new double3(0, 1, 0);
        double3 a = Vec.Unit(Vec.Cross(up, seed));
        double3 b = Vec.Unit(Vec.Cross(up, a));

        // Spaced closer together than they are wide, or it beads. Bounded so a large ring cannot
        // ask for thousands of spheres.
        int steps = (int)Math.Clamp(Math.Ceiling(Math.Tau * ringRadius / tubeRadius), 16, 160);
        Celestial? body = drape ? NearestCelestial(centreEcl) : null;

        for (int i = 0; i < steps; i++)
        {
            double angle = Math.Tau * i / steps;
            double3 at = centreEcl + ((a * Math.Cos(angle)) + (b * Math.Sin(angle))) * ringRadius;

            // Each bead sits on the ground under it, so the ring follows a slope instead of
            // burying one side and floating the other.
            if (body is not null && TrySnapToGround(body, at, out double3 ground, out _)) at = ground;

            if (TryEclToEgo(at, out double3 ego))
            {
                Program.GizmosRenderer.DrawSphere(ego, (float)tubeRadius, colour);
            }
        }
    }

    public static void DrawLineEcl(double3 startEcl, double3 endEcl, float4 colour)
    {
        if (Program.GizmosRenderer is null) return;
        if (!TryEclToEgo(startEcl, out double3 a)) return;
        if (!TryEclToEgo(endEcl, out double3 b)) return;
        Program.GizmosRenderer.DrawLine(a, b, colour);
    }

}
