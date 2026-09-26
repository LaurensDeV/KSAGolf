# KSA's frame order, and what instant a sample belongs to

What the engine does with time, read out of the decompiled sources rather than inferred from this
mod's behaviour. Every claim carries a `file:line` into `../ksa-game-assemblies/current/src`.
Build **2026.8.22.5348**. Line numbers move on every KSA update, so a citation that does not land
on what it claims means this file is behind the corpus, not that the corpus is wrong.

**Read this beside `docs/FRAMES-AND-EPOCHS.md`, not instead of it.** That file has the rules and
the four failure shapes; this one is the evidence under them. Every epoch rule in this mod was
first settled by internal consistency plus a flight — a form that agrees with itself and holds
still on screen — never by knowing what the engine does. This file closes that gap, for anything a
mod integrates on its own clock.

---

## The short answer

| What the mod assumes | Verdict | Where |
| --- | --- | --- |
| Samples read from the mod's hook are the **end of the step just applied** | **Confirmed** | §3, §4 |
| `Universe.GetLastSimStep()` is the interval those samples moved across | **Confirmed** | §2 |
| The mod's update is a postfix on `OnDrawUiViewports`, after the pass that built the frame's matrices | **Confirmed** | §1 |

**All three survive.** What follows from them for a body the mod integrates itself — that its
pre-step position is one applied step behind every world sample — is §5.

Four further things are worth knowing:

- **The step a mod is handed was sized from the previous frame's wall clock**, so a
  simulation-speed change cannot show up in it for two frames (§2).
- **`GetElapsedTime()` is not monotonic.** Loading a save rewrites the clock outright (§6).
- **The epoch need not be assumed at all** — `Orbit.StateVectors.StateTime` is public, and it is
  the sample's own timestamp (§8).
- **KSA advances its own particles by the step that has *not* run yet** (§7), which is not the one
  everything else moved by.

---

## 1. One frame, in order

`App.Run` is a bare `while` around `OnFrame(totalSeconds, dtPlayer)` with **no try/catch**
(`KSA/KSA/App.cs:35-54`); `dtPlayer` is wall-clock, clamped to
`1/GameSettings.Current.Simulation.MinTargetFrameRate` (`App.cs:40-41`). There is one simulation
step per frame and its length comes from that clamped `dtPlayer` — which is why there is no
interpolation alpha anywhere (§8).

`Program.OnFrame` (`Program.cs:2066-2162`), with the two StarMap hook points marked:

| # | Phase | Ref |
| --- | --- | --- |
| 1 | **`PrepareFrame`** — expanded below. **Every position in the world is advanced here and nowhere else.** | `Program.cs:2071` |
| 2 | `GaugeCanvas` / `BurnCanvas` on-frame | `Program.cs:2079-2086` |
| 3 | `GizmosRenderer.ResetInstances()` — anything submitted before this is discarded | `Program.cs:2088` |
| 4 | `PrepareImGui`, `OnFrameEditor` | `Program.cs:2089-2090` |
| 5 | **`OnFrameViewports`** — every controller writes its camera, then `Camera.OnFrame` builds this frame's view and view-projection matrices | `Program.cs:2091` |
| 6 | `OnDrawUiFrame` — **`[StarMapBeforeGui]` is a prefix here** | `Program.cs:2095` |
| 7 | `OnDrawUiViewports` — **`[StarMapAfterGui]` is a postfix here; this is where this mod simulates and draws** | `Program.cs:2096` |
| 8 | `OnDrawUiThreadSafe`, `DrawFps`, `OnDrawUiConsole`, `ImGui.Render()` | `Program.cs:2100-2114` |
| 9 | `OnFrameLaunchMenu`, `OnFrameHoveredOrbiters`, gauges | `Program.cs:2121-2138` |
| 10 | `OnFrameController`, `LightSystem.OnFrame`, `Cursor.UpdateInputRay` | `Program.cs:2143-2146` |
| 11 | `OnFrameCelestials` — camera-nearby body, altitude, terrain height. **Moves nothing** | `Program.cs:2148`, body at `:2480-2511` |
| 12 | `OnPreRender` → `Render` → `PostRender` | `Program.cs:2151-2159` |
| 13 | `FrameNumber++` | `Program.cs:2160` |
| 14 | *(StarMap postfix)* **`[StarMapAfterOnFrame]`** — after the render | `StarMap.Core/StarMap.Core.Patches/ProgramPatcher.cs:40-49` |

Steps 6 and 7 are inside `if (DrawUI)` (`Program.cs:2093-2097`) and step 8's `OnDrawUiThreadSafe`
inside a second one (`:2098-2101`); `DrawFps` and `OnDrawUiConsole` are not guarded. That guard is
the whole of §7.

`Viewport.OnFrame` is three calls — active controller, then `Camera.OnFrame`, then audio
(`Viewport.cs:145-150`); `Camera.OnFrame` builds `_vp.view` / `_vp.viewProjection` and extracts the
frustum planes (`Camera.cs:482-492`). So by the time the mod's hook runs at step 7, **this frame's
camera matrices are already built** and a pose written now is consumed at step 5 of the *next*
frame. That is the assumption `Ksa/LevelHorizonController.cs` and `IViewPose` exist to work around,
and it is confirmed.

### `PrepareFrame` — where the world moves

`Program.cs:2000-2064`, in order:

| Line | What |
| --- | --- |
| `2008` | `_screenshotCapture.OnPrepareFrame(...)` — **can set `Program.DrawUI = false`** (§7) |
| `2009-2010` | wait for the orbit and vehicle solver jobs queued **last** frame |
| `2011` | `Universe.ApplyOrbitSolvers()` — each `Celestial` takes its worker's new state vectors |
| **`2012`** | **`Universe.ApplyVehicleSolvers()`** — physics results applied, `_lastSimStep` advanced, `CurrentSystem.UpdatePerFrameData()` |
| `2019` | `InputEvents.ApplyInputEvents()` |
| `2026` | `RefreshVehiclesInFrame()` |
| `2045` | `Universe.ProcessAutoWarp(dtPlayer)` |
| **`2046`** | **`SimStep jobSimStep = Universe.GetJobSimStep(dtPlayer)`** — sizes the *next* step |
| `2047-2048` | `ExecuteNextVehicleSolvers` / `ExecuteNextOrbitSolvers` — queue the workers for it; `_nextSimStep = simStep` |
| `2049-2050` | `Network.Tick()`, `Glfw.PollEvents()` |
| `2051-2062` | two early returns: window closing, and a font rebuild (§7) |

On 2026.9.10.5438 `Glfw.PollEvents()` runs before `InputEvents.ApplyInputEvents()` rather than after
the solvers are queued; the rest of this order, and everything below that depends on it, is the same.

`Vehicle.PrepareWorker` is reached from `ExecuteNextVehicleSolvers` at `Program.cs:2047`, i.e.
*inside* `PrepareFrame` and before any mod hook of any kind runs. That is why an attitude command
written from a StarMap hook is overwritten and one written from a Harmony prefix on `PrepareWorker`
is not.

### A mod may destroy a vehicle from its hooks. It may not split one.

The same ordering decides this, and the two look identical until you try the second.

Every mod hook runs **after** `ExecuteNextVehicleSolvers` has queued the next worker run and
**before** the `ApplyVehicleSolvers` that will apply its results — which is the *next* frame's. So
a hook sits between a run being staged and its results being applied:

```
frame N   PrepareFrame:  wait -> ApplyVehicleSolvers -> ... -> ExecuteNextVehicleSolvers
frame N   mod hooks                                              <-- results staged, unapplied
frame N+1 PrepareFrame:  wait -> ApplyVehicleSolvers             <-- they land here
```

`KsaWorld.WaitForVehicleSolvers` joins that run, which is what stops the collection being mutated
underneath it. It does **not** apply the results. Destroying a vehicle from there is safe because a
destroyed vehicle's staged results have nowhere to land. **Splitting one is not**: the fragments
survive, and results computed against the whole craft are applied to them on the next frame.

Measured, failing seven parts of a 21-part drone from a hook: the drone fragmented into 11
vehicles and every subsequent frame threw

```
System.ArgumentOutOfRangeException: Index was out of range.
   at KSA.FlightComputer.UpdateTvcParams(FlightComputerOutput&)
   at KSA.PhysicsBubble.FullPhysicsPreStep(Double&)
```

for ever — `VehicleConfig.Gimbals` indexed against a state list belonging to a craft that no longer
exists. Nothing recovers, and the errors are on screen.

Re-running `Universe.ApplyVehicleSolvers()` from the hook is not the fix, though it is public: it
also advances `_lastSimStep` and calls `UpdatePerFrameData`, so calling it out of band moves the
clock every timing rule in this file rests on.

**The fix is to queue rather than apply.** `PartFailure.Detect` does not call
`PartFailureEvent.Apply` either — it writes `VehicleUpdateState.PartFailureEvent`, and
`ApplyRenderEventsToVehicles` applies it *immediately after* the staged results, inside the same
`ApplyResultsToMainThread`. A mod writing that same field has its part failures run on the engine's
own path at the engine's own instant. The field is public because that is the protocol; only the
`VehicleUpdateState` holding it is private, so reaching it takes a reflection, and losing that
reflection should fall back to destroying the whole craft.

Same run, same seven parts, after the change: zero exceptions.

### Nor may it stage one

The worker `ExecuteNextVehicleSolvers` queues is *running* while the hooks run, and nearly everything
it reads is a snapshot. One thing is not. For the controlled craft, while the engine-control gauge or
the staging list is open, `PhysicsBubble.RunVehiclePostWorkInner` calls
`Parts.PerformanceSequences.RecomputeForFlight` (`PhysicsBubble.cs:667-670`), which walks the live
`PartTree.SequenceList` — and `SequenceList.ActivateNextSequence` rebuilds it. A stage fired from a hook
races that walk. Seen once, with the gauge open:

```
Update task failed for vehicle(s): GeoSat FAT
System.NullReferenceException: Object reference not set to an instance of an object.
   at KSA.SequencePerformanceList.Recompute()
   at KSA.SequencePerformanceList.RecomputeForFlight(Single ambientPressure)
   at KSA.PhysicsBubble.RunVehiclePostWorkInner(VehicleUpdateState vehicleState)
```

Only the controlled craft, only with one of those two panels open, and only when the stage lands on
the walk, so it is rare. The safe place to stage is a `Vehicle.PrepareWorker` prefix, before the
worker is queued — the same window an attitude command needs.

---

## 2. `GetLastSimStep()` and `GetElapsedTime()`

```csharp
public readonly struct SimStep
{
    public required UniverseTime PreviousTime { get; init; }
    public required UniverseTime NextTime    { get; init; }
    public required double       DeltaTime   { get; init; }
}
```
`KSA/KSA/SimStep.cs`.

```csharp
public static SimStep     GetLastSimStep() => _lastSimStep;              // Universe.cs:2042-2044
public static SimStep     GetNextSimStep() => _nextSimStep;              // Universe.cs:2048-2050
public static UniverseTime GetElapsedTime() => _lastSimStep.NextTime;    // Universe.cs:2060-2062
public static double GetElapsedSeconds() => GetElapsedTime().Seconds();  // Universe.cs:2054-2056
```

**They are two fields of one struct, so they cannot disagree.** `GetElapsedTime()` is the end of
the interval `GetLastSimStep().DeltaTime` describes. `_lastSimStep` is written in exactly two
places: `ApplyVehicleSolvers` (`Universe.cs:1681`) and `DeserializeSave` (`Universe.cs:2112-2117`,
§6).

**The steps are contiguous — no gaps, no overlaps.** `GetJobSimStep` builds every step from the
end of the last one:

```csharp
public static SimStep GetJobSimStep(double dtPlayer)
{
    double num = _achievedSpeedFraction * GetSimulationSpeed();
    UniverseTime nextTime = _lastSimStep.NextTime;
    double num2 = dtPlayer * num;
    return new SimStep { PreviousTime = nextTime, NextTime = nextTime + num2, DeltaTime = num2 };
}
```
`Universe.cs:2264-2276`. So `PreviousTime(k) == NextTime(k−1)` by construction. **But `NextTime` is
`DeltaTime` rounded to the nanosecond** — `UniverseTime + double` goes through `Math.Round` — and every
celestial is placed on `NextTime` (`CelestialUpdateTask.Prepare`), so it is the difference of the
`NextTime`s, not the sum of the `DeltaTime`s, that reproduces the clock the planets are on. The two
differ by up to half a nanosecond a frame, which is 15 µm against 29.8 km/s; taken as the longer of the
two every frame it is 0.125 ns ahead on average and a ~20 mm walk over a long warped coast.
`Universe.GetAchivedSpeedFraction()`
(`Universe.cs:1977-1979`, and yes, that is the spelling) is public if the factor is ever wanted.

**The step is sized from the *previous* frame's wall clock, and that is the one genuinely new
fact here.** The step reported at frame *k* was built at frame *k−1* from *k−1*'s `dtPlayer`,
`_achievedSpeedFraction` and simulation speed, queued as `_nextSimStep` (`Universe.cs:1777`), and
promoted to `_lastSimStep` at frame *k* (`Universe.cs:1681`). Two consequences:

- **A simulation-speed change takes two frames to appear in the step.** Written during the mod's
  hook at frame *k*: read back from `Universe.SimulationSpeed` immediately at *k+1* (its setter
  writes `_simulationSpeed` synchronously, `Universe.cs:102-112`, `:1949-1957`), first used by
  `GetJobSimStep` at *k+1*, first
  *applied* at *k+2*. A mod that sets the speed and judges the result by the step must wait for
  that boundary: the speed reads back a frame before the step shows it.
- **`GetLastSimStep().DeltaTime` is not a measurement of anything the mod can time around itself.**
  It is the interval the world was integrated across, decided a frame before the mod sees it. It
  cannot be a phase out from the samples, because the samples *are* its endpoint.

`Universe.IsPaused()` is `_simulationSpeed == 0.0` (`Universe.cs:1595-1597`) — a statement about
the setting, read the instant it is written, while `_lastSimStep` still carries the step queued
before it. That is the one-frame skew `FRAMES-AND-EPOCHS.md` warns about, confirmed: **gate on the
applied step, never on the flag.**

---

## 3. What instant a `Celestial`'s position is

**The end of the step just applied, i.e. `Universe.GetElapsedTime()`.** This is the most
load-bearing assumption in the mod and the source settles it outright.

The orbit worker is handed a `SimStep` at `ExecuteNextOrbitSolvers` (`Universe.cs:1752-1765`, via
`Celestial.PrepareWorker` at `Celestial.cs:1643-1646`) and evaluates the orbit at that step's
**`NextTime`**:

```csharp
private void DoWorkAndStageResults()
{
    if (!(_simStep.DeltaTime <= 0.0))
    {
        UniverseTime nextTime = _simStep.NextTime;
        Orbit orbit = _readOnlyCelestial.Orbit;
        NewStateVectors = orbit.GetStateVectorsAt(nextTime);
        ...
    }
}
```
`KSA/KSA/CelestialUpdateTask.cs:50-61`.

Those results are taken at `ApplyOrbitSolvers` (`Universe.cs:1638-1653` →
`Celestial.UpdateFromTaskResults`, `Celestial.cs:1648-1666`), and the cached ecliptic position is
rebuilt from them a few lines later in `Celestial.UpdatePerFrameData`
(`Celestial.cs:593-608`), which is what `GetPositionEcl()` / `GetVelocityEcl()` return
(`Celestial.cs:388-408`). The step whose `NextTime` that was is the one promoted to `_lastSimStep`
at `Universe.cs:1681`. **So `celestial.GetPositionEcl()` read from the mod's hook is the body's
position at `Universe.GetElapsedTime()`, exactly.**

Two details worth having:

- **A zero-length step does not move a celestial and does not need to.** `DeltaTime <= 0` skips the
  evaluation entirely (`CelestialUpdateTask.cs:52`), leaving the old state vectors — and
  `NextTime == PreviousTime` for such a step, so the old vectors are still stamped at the current
  elapsed time. Paused, the sample and the clock still agree.
- **The whole system shares one epoch**, because it is walked in one call — see §4.

---

## 4. Same question for `Vehicle` — and it does not differ

**Identical, and by construction rather than by coincidence: celestials and vehicles are advanced
in the same call.**

`ApplyVehicleSolvers` ends with three lines (`Universe.cs:1681-1683`):

```csharp
_lastSimStep = _nextSimStep;
_lastPlayerDeltaTime = _nextPlayerDeltaTime;
CurrentSystem.UpdatePerFrameData();
```

`CelestialSystem.UpdatePerFrameData` (`CelestialSystem.cs:260-271`) finds each root — an
`IParentBody` that is not an `IOrbiter` — and calls `UpdatePerFrameDataTree()`, which is
parent-first and covers **every celestial and every vehicle in the system**
(`IParentBody.cs:110-124`). Vehicles are the leaf case at `IParentBody.cs:121`.

`Vehicle.UpdatePerFrameData` (`Vehicle.cs:2456-2490`) then does the same composition a celestial
does:

```csharp
doubleQuat cci2Cce = Parent.GetCci2Cce();
_positionCce = Orbit.StateVectors.PositionCci.Transform(cci2Cce);
_velocityCce = Orbit.StateVectors.VelocityCci.Transform(cci2Cce);
_positionEcl = Parent.GetPositionEcl() + _positionCce;
_velocityEcl = Parent.GetVelocityEcl() + _velocityCce;
```

and `GetPositionEcl()` / `GetVelocityEcl()` return those fields (`Vehicle.cs:891-900`). Because the
walk is parent-before-child, the parent's `_positionEcl` is already this frame's when a child adds
it. **One epoch for the whole system, and it is `GetElapsedTime()`.**

The state vectors behind it come from the physics worker, which integrates from `SimStep.PreviousTime`
to `SimStep.NextTime` — the last sub-step is clamped to `SimStep.NextTime` explicitly
(`PhysicsBubble.cs:1339-1354`), and the analytic and freefall paths are applied at `SimStep.NextTime`
(`PhysicsBubble.cs:1064`, `:1085`, `:2329-2349`). Results reach the `Vehicle` through
`BubbleApplyResultsJob` → `PhysicsBubble.ApplyResultsToVehicles` (`PhysicsBubble.cs:628-656`) →
`Vehicle.UpdateFromTaskResultsUnsynchronized` (`Vehicle.cs:2247-2355`), which writes both
`_kinematicStates` (`:2253`) and `Orbit.UpdatePosition(...)` (`:2280`).

**CLAUDE.md's line about `PrepareFrame` advancing vehicle positions before the viewport pass is
confirmed** — `Program.cs:2012` is 79 lines and one whole phase ahead of `Program.cs:2091`. So a
camera following a vehicle reads a position one step newer than anything the mod computed against
that vehicle at its previous hook, and an offset built there lands a frame of the vehicle's motion
out.

**The analytic and physics positions of a vehicle share an epoch; they differ in *frame*, not in
time.** `_kinematicStates` and `_positionEcl` are both written during the same
`ApplyVehicleSolvers`. So the metres by which the two disagree on a landed craft are a
bubble-origin difference and never a timing one, and no amount of re-phasing will close them.

---

## 5. A body the mod integrates is one step behind the world

At frame *k*, the mod consumes the step **just applied** (`KsaWorld.ConsumeSimStep`) and integrates
whatever it simulates across it. So, from §2's contiguity:

| | epoch |
| --- | --- |
| the mod's body **before** its step | `NextTime(k−1)` |
| the mod's body **after** its step | `NextTime(k)` |
| every celestial and vehicle sample | `NextTime(k)` |

The world sample matches the body's **post**-step instant, not its pre-step one. Anything read at the
pre-step position and differenced against `body.GetPositionEcl()` — altitude, air density, the
ground's velocity, gravity — is short by `bodyVelocityEcl * dt`: ~0.9 km at 1× and ~3.9 km at 8×.
Back-dating the world sample by `elapsedInFrame - frameSeconds` (`−frameSeconds` at the first
sub-step), or equivalently moving the body forward by `bodyVelocityEcl * secondsIntoFrame`, puts the
pair on one instant.

**That correction is right for a term read *at* a position and not obviously right for a term that
*is* a field about a body.** For density it is worth kilometres to a fast body: altitude is a
radial reading against an 8 km scale height, where the offset does not cancel. For the ground's
velocity it is negligible — the offset enters only through `spin × fromCentre`, 0.3 m/s at 4 km. For
gravity, moving the *sample point* translates the whole field, pulling toward a centre displaced
from the one the ground test uses, and flown that loses; what works moves the field's **centre** to
the body's own instant and leaves the sample point alone.

A constant step cannot tell any of these forms apart, so a headless test will agree with whichever
one it was written against; §10.

---

## 6. The one place the clock is not continuous

`Universe.DeserializeSave` — loading a save — rewrites the step outright
(`Universe.cs:2111-2127`):

```csharp
UniverseTime universeTime = universeData.GameTime;
_lastSimStep = new SimStep { PreviousTime = universeTime, NextTime = universeTime, DeltaTime = 0.0 };
_nextSimStep = _lastSimStep;
```

then re-evaluates every celestial at the new elapsed time. So:

- **`GetElapsedTime()` is not monotonic.** It can jump forward by years or backward by any amount.
- **The jump announces itself**: `DeltaTime == 0` while `NextTime` differs from the previous
  frame's. No step `GetJobSimStep` produces can look like that, because it always starts at
  `_lastSimStep.NextTime`.
- **This mod resets on it.** `Ksa/WorldReloadHook.cs` postfixes `Program.OnGameLoaded`, which runs
  immediately after `DeserializeSave`, and `KSAGolfMod` then calls `KsaWorld.ResetSimStepTracking()`
  so `StepGate` does not carry the old `_integratedThrough` across. Without the reset a backward jump
  is harmless — `StepGate.Consume` takes the span only within a nanosecond of the reported step or
  longer, so a negative span is ignored and the reported `DeltaTime` of 0 is taken — and a forward
  jump hands the mod an enormous span, which `SimClock.Classify` should reject as `Skipped`.

---

## 7. When the mod's hook does not run at all

`OnDrawUiFrame` and `OnDrawUiViewports` are both inside `if (DrawUI)` (`Program.cs:2093-2097`), so
**a `[StarMapBeforeGui]` or `[StarMapAfterGui]` method is not called at all when `Program.DrawUI`
is false** — a postfix on an uncalled method never runs. `ScreenshotCapture.OnPrepareFrame` sets it
false for a hi-res capture without the HUD (`ScreenshotCapture.cs:228-232`, restored at `:425` and
`:450`), and the debug key at `Program.cs:1696` toggles it. `PrepareFrame` runs regardless
(`Program.cs:2071`, and it is where the capture arms itself), so **the world still advances across
the skipped frame**.

There is a second route: `PrepareFrame` returns `Exit` on a window close (`Program.cs:2051-2055`)
or a font rebuild (`:2056-2062`), and `OnFrame` returns immediately (`Program.cs:2071-2076`). Both
of those returns are **after** `ApplyVehicleSolvers` and after `ExecuteNext*Solvers`, so again: the
step was applied and the mod never saw it. The font-rebuild case is recoverable and the game
carries on.

That is what `KsaWorld.ConsumeSimStep` handing `StepGate` the *span* between step boundaries — not
the last `DeltaTime` alone — is for, and it is the right shape: the step boundaries are contiguous
(§2), so the span across any number of missed frames is exact.

**What this mod does about it.** `Sim/FrameLatch.cs` hands the frame's work to whichever hook
reaches it first, so the simulation never stops. That alone leaves the step landing at #14, after
the render, so everything written into the world — subpart transforms, effects — is drawn on the
following frame; at 250 m/s that is 4 m of a body's own travel, and it alternates with the
display's frame pacing rather than sitting still. `Ksa/PreRenderHook.cs` closes it with a **prefix**
on **`OnFrameCelestials`** (#11), the last phase before `OnPreRender` that is not behind the
`DrawUI` guard. Every candidate is private, so the patch resolves by name and **degrades to #14 if
it ever fails**, which is the behaviour without it.

**A prefix and not a postfix, and that is not a detail.** #11 resolves the camera-nearby body and
updates the planet's shader data for the frame, so a step taken after it prepares the planet
against a camera the mod is about to move — the ground renders as open water on pressing F2 while
the mod is driving the camera. With the UI drawn the mod steps at #7, which is ahead of #11;
a prefix is what puts a hidden-UI frame on the same side of it. **The hook exists to make the two
paths identical, and a postfix quietly did not.**

Two gaps it does not close, both covered by the #14 fallback: the early return at `:2119` is ahead
of #9, so a frame taking it reaches none of #9–#12; and `PrepareFrame` returning `Exit` skips
everything from #2 on.

**`Program.FrameNumber` (`Program.cs:281`) is not a reliable skipped-frame detector.** It is
incremented at `Program.cs:2160`, past three early returns (`:2075`, `:2119`, `:2156`), so it counts
frames that reached the end of the render rather than frames that were begun. It does increment on
a `DrawUI == false` frame, so it catches the screenshot case and not the `PrepareFrame`-exit one.
The step boundary is the better question in both.

**One asymmetry worth knowing if a mod places particles:** KSA advances its own particle system by
`Universe.GetNextSimStep().DeltaTime` (`Program.cs:2221-2228`) — the step that has *not* run yet —
while every position in the frame moved by the step that has. Those differ whenever the frame time
or the simulation speed changes. A mod placing emitters at positions it computes itself is not
affected; one relying on KSA to advance them against a moving body is.

---

## 8. What a mod can read instead of guessing

**Yes — the engine stamps the sample.** `StateVectors` carries its own time:

```csharp
public struct StateVectors
{
    public readonly UniverseTime StateTime;
    public readonly double3 PositionCci;
    public readonly double3 VelocityCci;
    ...
}
```
`KSA/KSA/StateVectors.cs:6-17`.

The chain to it is public the whole way: `Vehicle.Orbit` (`Vehicle.cs:368`), `Celestial.Orbit`
(`Celestial.cs:71`), `IOrbiter.Orbit` (`IOrbiter.cs:16`), and
`Orbit.StateVectors` as `public ref readonly` (`Orbit.cs:1162`). So

```csharp
vehicle.Orbit.StateVectors.StateTime == Universe.GetElapsedTime()
```

is a **checkable** statement rather than an assumption, per craft, per frame — and it is the
assertion a diagnostic should make rather than a comment restating §3 and §4. Under full physics
the orbit is rebuilt from the physics state at that state's own time
(`PhysicsBubble.cs:2588-2605`), so the stamp stays honest on the path where the analytic orbit is
being regenerated every step.

The rest of what is reachable:

| Want | Read | Ref |
| --- | --- | --- |
| the epoch of the world's samples | `Universe.GetElapsedTime()` | `Universe.cs:2060-2062` |
| the interval they moved across | `Universe.GetLastSimStep()` | `Universe.cs:2042-2044` |
| the step now in the workers, applied next frame | `Universe.GetNextSimStep()` | `Universe.cs:2048-2050` |
| the epoch of one specific body | `x.Orbit.StateVectors.StateTime` | `Orbit.cs:1162`, `StateVectors.cs:8` |
| how much of the requested speed the solver is keeping up with | `Universe.GetAchivedSpeedFraction()` | `Universe.cs:1977-1979` |
| a frame counter | `Program.FrameNumber` — with §7's caveat | `Program.cs:281`, `:2160` |

**There is no interpolation alpha, and there is nothing to interpolate.** The simulation step is
derived from the frame's own `dtPlayer` (`Universe.cs:2266-2268`), so there is exactly one step per
frame and the rendered state *is* the integrated state — `Camera.GetPositionEgo`
(`Camera.cs:231-245`) differences stored positions and blends nothing. A fixed-timestep engine
would need an alpha; this one does not have the problem.

**`Vehicle.KinematicStates` carries no timestamp** (`KinematicStates.cs:8-20`, public accessor at
`Vehicle.cs:532`). Its epoch has to come from §4 — which is fine, because it is the same one.

---

## 9. The three assumptions, one by one

**1. Samples read during the mod's hook are the end of the step just applied.** **Confirmed.**
Celestials are evaluated at `_simStep.NextTime` (`CelestialUpdateTask.cs:54-56`); vehicles are
integrated to `SimStep.NextTime` (`PhysicsBubble.cs:1339-1354`); both are composed into `_positionEcl`
by one tree walk at `Universe.cs:1683`, immediately after `_lastSimStep` is advanced at `:1681`; the
mod's hook is 84 lines and six phases later at `Program.cs:2096`; and nothing between them moves
anything (`Program.cs:2078-2095`, and `OnFrameCelestials` at `:2480-2511` only reads).

So the world sample is exactly one applied step ahead of a mod-integrated body's pre-step
position, and `elapsedInFrame - frameSeconds` is the correct back-date (§5) — provided
`frameSeconds` is the step the mod actually consumed rather than the last `DeltaTime`.
`KsaWorld.ConsumeSimStep` returns the former (§7).

**2. `GetLastSimStep()` reports the step just finished applying.** **Confirmed**, and it is
stronger than that: `GetElapsedTime()` *is* that step's `NextTime` (`Universe.cs:2060-2062`), so the
interval and the epoch are the same object and cannot drift apart. Steps are contiguous
(`Universe.cs:2267`), so the mod's `StepGate` deduplicating on `NextTime` is deduplicating on a key
the engine guarantees is unique per step and ordered.

**3. The mod's update is a postfix on `OnDrawUiViewports`, after the viewport pass.**
**Confirmed.** `ProgramPatcher.AfterOnDrawUi` is `[HarmonyPatch("OnDrawUiViewports")]
[HarmonyPostfix]` (`ProgramPatcher.cs:29-38`); `OnFrameViewports` — which runs every controller and
then `Camera.OnFrame` to build the matrices — is at `Program.cs:2091`, five lines earlier
(`Viewport.cs:145-150`, `Camera.cs:482-492`). The gizmo reset at `Program.cs:2088` is before it and
the render at `:2151` is after it, which is why the mod draws from this hook and not from
`[StarMapAfterOnFrame]`.

---

## 10. What this does not settle

- **§5 is a phase, not a verdict on every term.** The source says the phase is one applied step
  wide; what closing it is worth depends on the term, and only flying says which. A constant step
  cannot see a phase error at all, so no headless suite settles it.
- **`_achievedSpeedFraction` is a feedback loop on solver load** (`Universe.cs:1659-1679`), smoothed
  with a 0.9/0.1 filter and ratcheting downward instantly. It means the step can shrink without
  either the frame time or the simulation speed moving. Nothing in this mod reads it; anything that
  calibrates off the step it was handed rather than off a frame rate is the right shape regardless.
- **Sub-frame ordering inside the physics worker is out of scope here.** What matters to a mod is
  that the worker's output is stamped at `SimStep.NextTime`, and that is §4.
- **This is one build.** `tools/ksa-api-diff.sh` cannot settle any of it: what moves here is
  meaning rather than signature, so it compiles clean and is wrong in flight. `Program`,
  `Universe`, `Vehicle` and `Celestial` are all types the mod binds to, so their files do land in
  its changed-file list — reading them is the whole of the check. Re-read §2 and §4 after a KSA
  update the way `docs/BLOCKED-ON-KSA.md` is re-read.
