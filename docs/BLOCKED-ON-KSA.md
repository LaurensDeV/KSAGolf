# Blocked on KSA

Things a mod wants and **cannot build**, because the game does not expose what they need. Not a
backlog: everything here waits on RocketWerkz.

Each entry cites what the decompiled corpus says, so a claim can be rechecked after a KSA update
rather than taken on trust. **Recheck this file when the game moves** — the whole point of it is
that some of these will quietly become possible.

Findings are against KSA **2026.9.22.5482**. Paths are relative to
`../ksa-game-assemblies/current/src`.

## Recheck after a KSA update

Tick when rechecked against the new build, then untick for the next one. `tools/ksa-api-diff.sh`
will not surface any of these — none is a signature change, and most are a call that does not
happen rather than a member that moved.

- [x] Secondary viewport gets the planet, atmosphere and lighting passes
- [x] `Camera.NearbyCelestial` is set per camera rather than only for the frame viewport
- [x] Wheel, suspension or steering module exists
- [x] Per-mod vehicle library path, or a way to register saved craft
- [x] `UncompressedVehicleSave.Load` honours `Character`, making a kitten launchable
- [x] Custom part modules can be registered without patching
- [x] Public accessor for the volumetric trail renderer
- [x] A post-processing or full-screen shader hook a mod can register into
- [x] **A hook between applying the vehicle solvers and snapshotting them** — a mod commanding a
  vehicle's attitude can drop its `PrepareWorker` patch the day this exists
- [x] **A menu-bar hook a mod can register into** — delete `Ksa/Ui/ModMenuEntry.cs` the day this exists
- [x] **`DistanceReference.IsValid()` stops requiring 100 km** — go back to `IsValid()` on the atmosphere and the ocean the day it does

**Rechecked against 2026.9.22.5482: every open item above still blocked, and one turned out never
to have been** — a character attachment can be aimed through its own `CosmeticAttachment.Transform`,
which the renderer composes rather than overwrites (`docs/KITTEN-ARMS.md`). There is no new StarMap
hook.

**Physics bubbles.** Since 2026.9.10.5438 `ComputeDerivatives` applies the centrifugal term to every
member of a `Ccf` bubble, and Coriolis to every member not in contact, keyed on the bubble's frame
rather than on `InPhysicsRadius` (`PhysicsStates.cs:869-882`); since 2026.9.22.5482
`TryToPutOnRails` returns a vehicle to rails from any bubble frame, not only `Cci` (`:818-839`).
Before that, a high vehicle in a rotating bubble led by a heavy landed one was integrated with its
fictitious forces switched off.

**Part failure and explosions.** A part's crash tolerance is derived from its collider volume
against a 9 MPa base at 330 kg/m³ and clamped to 0.1–100 MPa (`PartStructuralLimits.cs:8-39`,
`Part.cs:843-858`), and every queued failure and every `DestroyVehicleFromEvent` spawns an engine
explosion (`PartFailureEvent.cs:38-67`, `Universe.cs:1909-1917`). The explosion system is public:
`ExplosionFlashSystem.Spawn` places a point light that every viewport draws
(`ExplosionFlashSystem.cs:26`, `:73-91`; eight slots shared with the engine), and
`ExplosionSystem.SpawnPreset` fires a declared `<Explosion>` — emitters, volumes, a flash and a
sound — from an anchor with no vehicle (`ExplosionSystem.cs:217`, `:471-476`). Its volumes draw
through the volumetric trail renderer, over the camera's nearby body with or without air, behind
their own `Graphics.Explosions` setting (`ExplosionVolumeSystem.cs:47-50`,
`VolumetricTrailRenderer.cs:246`, `PlanetTransparenciesRenderer.cs:316-326`), and its intensity
clamps at 100x of 5e10 J.

**Re-derived against 2026.9.22.5482:** the citations into `Program.cs` in the secondary-viewport
and attitude-command entries, the plume gate in `Vehicle.cs` and `RocketNozzle.cs`, the raycast in
`Part.cs`, and those in this header. The secondary-viewport entry's list of what such a window does
and does not render was rechecked against it and holds.

The other line numbers below are against **2026.8.22.5348** and have not been re-derived: 2026.9.4.5400
replaced the `Viewport` class with `IViewport` / `ViewportBase` / `GameViewport` and moved the list
into `ViewportRegistry`, which moved most of the render path. The *claims* were rechecked against
the new corpus; only the citations are stale. Lighting is still a per-viewport choice —
`IViewport.LightMode` is a `ViewportLightMode` (renamed from `EViewportLightMode`), set to
`Clustered` for the main viewport and `None` for the thumbnail one — but that is the pass a
secondary viewport was already getting some of. `OnFrameCelestials` still resolves one camera through `GetCamera()`
and still calls `_planetRenderer.OnFrame(FrameViewport, ...)`, so the planet, atmosphere and ocean
passes remain the frame viewport's alone and the first two entries stand. `UncompressedVehicleSave.cs`
does not mention `Character` at all; `KittenRenderable` writes an attachment's transform and
submits its draw in consecutive statements; `KSA.Rendering.PostProcessing` is an anti-aliasing
pass and a tone curve.

---

## A menu bar a mod can add to

**Delete the workaround the moment this changes.** `src/KSAGolf/Ksa/Ui/ModMenuEntry.cs` exists
only because of what follows, is wanted gone, and is on the recheck list above so it is looked at
every time the game moves.

**Wanted.** An entry in KSA's own menu bar, so the panel opens from where a player expects rather
than from a floating button parked over the flight gauges.

**The engine reason.** `Program` draws the bar inline —
`ImGui.BeginMenu("File")`, `"Universe"`, `"View"`, `"HUD"` — with no event, no registry and no
extension point of any kind. StarMap adds nothing either; its attributes are lifecycle hooks and
none is menu-shaped.

**What the ecosystem does instead**, and why it is unpleasant. MrJeranimo's **ModMenu** Harmony
*transpiles* `Program.DrawMenuBar`, scans its IL for an `ImGui.EndMenu()` call and splices in its
own `BeginMenu("Mods")`. Mods opt in with a `[ModMenuEntry]` attribute, which ModMenu matches by
`GetType().Name` alone — so a mod copies the attribute rather than referencing anything, which is
all `Ksa/Ui/ModMenuEntry.cs` is. It costs no dependency, but the whole arrangement stands on
rewriting the IL of a game method in a pre-release build. `DrawMenuBar` being public buys nothing
here: calling it draws the bar, it does not contribute to one.

The mod also appends to the bar directly with `ImGui.BeginMainMenuBar()`, which works because
ImGui's menu bar is immediate-mode. It must be called *before* KSA's GUI pass: from
`[StarMapAfterGui]` the bar has already been ended for the frame and the call returns false. That
is still ImGui behaviour rather than a supported hook, which is why the floating button stays.

**What would unblock it.** Any public means of contributing a menu: an event on `Program`, a list
of callbacks, or an asset type. Then both the copied attribute and the `BeginMainMenuBar` append
go, and the panel opens from a menu the game itself put there.

## Full-screen post-processing shaders

**Wanted.** Anything drawn on the framebuffer rather than in the world: a tint, a vignette, grain,
a false-colour view.

**The engine reason.** `KSA.Rendering.PostProcessing` contains `Cmaa2Renderer` and
`HableFilmicToneCurve` and nothing else: an anti-aliasing pass and a tone curve, both wired
directly into `Program`. There is no chain to append to, no asset type for a shader, and nothing
in the namespace a mod can register with. Searching the corpus for `GlobalPostShader` or any
equivalent returns nothing, so the extension point does not exist to be called.

**It is nonetheless being done, by patching.** AMPW's **ShaderExtensions** adds `<ShaderEx>`,
`<PostProcessingShader>`, `<GlobalPostShader>` and `<ImGuiShader>` asset types to KSA, built on
tsholmes' **KittenExtensions**. His **KSAGEffects** then declares a fragment shader with

```xml
<GlobalPostShader Id="GEffectFrag" Path="Shaders/GEffectShader.frag" RenderPassId="256" SubpassId="0">
  <GEffectBuffer Id="GEffectBuffer" Size="1" />
</GlobalPostShader>
```

and the shader reads the framebuffer as a genuine Vulkan **subpass input**
(`layout(set = 1, binding = 0, input_attachment_index = 0) uniform subpassInput Source`), with a
uniform buffer the mod fills each frame.

What comes with that route:

- **Full-screen and main window only.** `<PostProcessingShader>` and `<GlobalPostShader>` differ
  only in whether they run before or after ImGui; ShaderExtensions' README is explicit that "the
  shaders only target the main window, any other windows are ignored". There is no per-viewport
  post-process.
- **A post shader cannot be turned off.** It runs every frame at its stage forever. The documented
  answer is to pass an amount through a uniform and return the source colour unchanged at zero,
  which is a one-line early-out and costs a full-screen pass of nothing.
- **RenderPassId is a bare integer with no registry.** Ordering is only defined for unique
  renderpass/subpass combinations, and two mods that pick the same number get undefined execution
  order rather than an error. AMPW's g-effects already occupies 254, 255 and 256.
- **It sees colour, not objects.** A subpass input is the composited image, so anything needing to
  know what a pixel *was* — per-object data KSA never computes — needs a replacement material
  shader or a second render target. `<ShaderEx>` adds bindings to *existing* fragment shaders and
  is the only plausible route; whether it can carry such data is **unverified**.

**So this is blocked on KSA only in the sense that doing it natively is impossible.** Taking it
would mean depending on two third-party framework mods, which is a different decision from a
missing engine feature — this mod currently requires only StarMap. Recorded here so the trade is a
deliberate one.

## Secondary viewport: no sky, clouds, atmosphere or terrain

**Wanted.** A second camera window, so one thing can be watched while the player flies another.
The camera, its aim and the window all work; the *picture* is wrong.

**What happens.** A secondary viewport shows a raw starfield above a hard horizon and a
featureless grey ball, where the main view at the same position shows sky, clouds and terrain.

**Why.** Secondary viewports go through `Program.RenderViewport` (`KSA/KSA/Program.cs:4415-4551`),
a much shorter path than the main one. The loop that calls it ends at `Program.cs:4607`, and the
next statement is `_renderedViewport = MainViewport` (`:4609`) — so every pass after it is pinned
to the main view whatever else is open: the planet renderer, the light and shadow passes, the
ocean (`:4783`), and `_planetTransparenciesRenderer.Render` (`:4792`), the sole call site of the
atmosphere and cloud compute passes anywhere in the game.

**`ViewportOptionFlags.RenderAtmosphere` is set on all four secondary viewports and does not mean
this** (`Program.cs:949`). Inside `RenderViewport` it gates one call, `_sunbloomRenderer.Render`
(`:4499-4509`), and nothing else. The flag is the first thing that looks like the fix and is not.

Two details explain the exact image:

- The starfield is drawn because stars *are* in the reduced path (`Program.cs:4458-4469`).
- The grey ball is not terrain. It is `StaticCelestial.RenderSphere` → `DistantSphereRenderer`, a
  sphere scaled to `MeanRadius` with no heightfield. It appears because
  `Camera.NearbyCelestial` is only ever assigned inside `OnFrameCelestials`
  (`Program.cs:2696-2721`), which runs for the frame viewport — always the main one. The check
  that suppresses the planet you are standing on
  compares `camera.NearbyCelestial == orbiter` (`StaticCelestialDistanceRendering.cs:416`), and a
  secondary camera's is permanently `null`, so it never matches. The same `null` zeroes that
  viewport's lighting data.

**Why a mod cannot fix it.** `PlanetTransparenciesRenderer`, `OceanRenderer` and
`OverallBloomRenderer` are constructed holding `Program._offscreenTarget`
(`Program.cs:1161, 1180, 1189`), which *is* `MainViewport.OffscreenTarget` — attached as a
shared target at `Program.cs:1523`. The `IViewport` they accept per call only selects a shader
dynamic offset; the image they write into was baked into their descriptor sets at construction.

**Redirecting them is not the blocker, though, and an earlier version of this entry said it was.**
`PlanetRenderer`'s constructor is public and takes an `IRenderPassInfo`
(`PlanetRenderer.cs:462`); `PlanetTransparenciesRenderer.RebuildFrameResources(RenderTarget,
RenderTarget, RenderTarget)` is public (`:304`); every dependency is reachable
(`Program.GetRenderer`, `GetCloudShadowsRenderer`, `SunShadowSystem`,
`Instance.TextureSystem`); and `Renderer.ComputeCommandBufferPool` and `TrySubmitFrame` are public
too. A second set bound to a leased secondary viewport's own `OffscreenTarget` is reachable in
principle.

**The blocker is that nothing lets a mod record into the frame.** Every StarMap hook takes
`(double currentPlayerTime, double dtPlayer)` and no more — `BeforeMain`, `BeforeGui`, `AfterGui`,
`AfterOnFrame`, `AllModsLoaded`, `ImmediateLoad`, `Unload`. The passes must be recorded between
`BeginRendering` and `EndRendering` on the target, inside the private `Program.RenderGame`. Two
further costs sit behind that even if it were patched: `SunShadowSystem` and
`_cascadedShadowSystem` fit their cascades to the frame camera, so a second view's terrain would
carry the first view's shadows, and the whole thing roughly doubles the heaviest GPU work in the
frame. **The failure mode is what settles it** — a mis-set descriptor in a hand-built pass is a
device-lost, where a Harmony patch that fails to apply degrades to a log line or a build error.

**What would unblock it**, roughly easiest first:

1. Assign `Camera.NearbyCelestial` per camera rather than only for the frame viewport. On its own
   this removes the wrong grey sphere. Cheap.
2. Array the atmosphere and cloud LUTs by viewport as well as by planet. There is already a
   pattern for this in the codebase — `SunbloomRenderer`'s buffers are sized by the viewport
   count, which is `ViewportRegistry.MAX_VIEWPORTS` and a viewport's own `ShaderSlot` since
   2026.9.4.5400.
3. Let the transparency, ocean and bloom renderers take their render target per call, or hold one
   set per viewport.
4. Run the planet, atmosphere and lighting passes inside the per-viewport loop.

**The architecture already supports per-viewport wherever it was designed to** — `_lightingData`
is indexed by `viewport.Index`, and the sunbloom buffers by viewport count. This is unfinished
rather than impossible.


**Workaround: take over the main viewport instead** — fully public API, full render quality,
borrowed and handed straight back (`KsaWorld.TryLookFromMainViewport`). A secondary window stays
useful for what it does draw.

**What the secondary window *does* get is worth knowing, because it decides what it is good for.**
`RenderViewport` draws stars and the Milky Way, the distant-sphere pass, vehicles, part models,
static objects, the translucency pass, orbit lines and gizmos, then composites through that
viewport's own CMAA2 and tonemap. So craft, mod-drawn bodies and their trails all draw normally; it
is the *ground* that is missing. A window watching something in the air or in orbit is usable. One
watching something on the ground is not.

**Confirmed in flight**, and the picture is the proof: a camera window on the pad shows the launch
complex and nothing else — the pad is a `StaticObject`, which `RenderViewport` does draw
(`StaticObjectRenderer.WriteCommands`), while the field it stands in is terrain, which it does
not. So "the ground" appearing is not evidence the block has lifted; only a hillside away from a
structure is.

## A second planet renderer was built and flown, and it does not work — the mesh is the reason

**Do not try this again without reading this section.** The retargeting argument above is sound as
far as it goes: `PlanetRenderer`'s constructor is public and takes an `IRenderPassInfo`, a
`RenderTarget` is one, every viewport past the main one owns its own, and both injection points
exist — a prefix on the private `Program.RenderViewport` for the work Vulkan forbids inside a
render pass, and one on the public `StaticObjectRenderer.WriteCommands` for the draw, which is
called between `BeginRendering` and `EndRendering` on the right target. All of that was built, and
the log said so: *both render hooks are in*, *a second
planet renderer is up on Camera 3 (500x500)*, no exception.

**It produced no terrain in the window and made the main view's ground and trees flicker.**

The reason is a level below the render targets. `PlanetRenderer.GetMesh` is
`celestial.GetLodMesh(_meshLodIndex)`, and `GenerateMeshData` dispatches a compute pass that writes
into `cubeMesh.Mesh`'s own vertex and index buffers (`PlanetRenderer.cs:2131`, `:1782-1800`). **The
terrain mesh belongs to the `Celestial`, not to the renderer** — there is exactly one per body, and
it is regenerated every frame for one camera. A second renderer does not get its own; it writes the
same buffers with a different camera's LOD and a different texture anchor, and the two alternate.

So the engine is not merely *unfinished* per-viewport, as the list above implies: below the target
binding it is **single-camera by construction**, and a per-viewport render needs per-viewport
terrain mesh state that does not exist to be allocated. That does not change what would unblock it
— it adds to it. Item 4 has to bring the mesh with it.

**A wireframe of the sampled height field, projected through the window's camera and drawn as ImGui
polylines, was also built and dropped, and that one worked.** Safe by construction — it touches no
render pass and cannot flicker anything — but wire on the glass occludes nothing, so a craft behind
a hill still draws in front of it, and a square a couple of kilometres across fades to nothing well
short of a horizon. **The drawing was never the hard part**: a view that cannot hide what is behind
terrain is not much more use than no terrain at all, and only the engine can give depth.

---

## Driving the main camera: set CameraRotation, do not unfollow

**Not blocked** — recorded because getting it wrong is a crash, in engine code, with nothing in
the message pointing at the cause, and because the obvious reading of it is backwards.

`FixedController.OnFrame` runs only when the camera it drives is following something, and then:

```csharp
double3 cameraRotation = CameraRotation;                       // public field, defaults to zero
double3 vector2 = double3.Cross(cameraRotation, vector).Normalized();
...
Camera.PositionEcl = following.GetPositionEcl() + CameraOffset;
```

So Fixed mode is **"follow this, but sit at an offset from it and look along a direction the
caller supplies"** — `CameraOffset` and `CameraRotation` are the entire interface, and the offset
is measured from the followed craft rather than from the world. A camera in Fixed mode *should* be
following something.

It divides by zero only because `CameraRotation` defaults to the zero vector, and crossing that
with anything and normalising is a division by zero length. **Set `CameraRotation` before setting
the mode**, and keep it set every frame.

The tempting misreading is that Fixed and following are an illegal pair, and that the fix is
`Camera.Unfollow(changeControl: false)` first. That does stop the crash, and it is wrong: the
camera then has nothing to be offset from, the view has to be restored by re-attaching a follow,
and *any* other thing in the game that attaches one — a jump-to-vehicle key, a scene teardown —
puts the camera back into the fatal pair with the rotation still zero.


`KsaWorld.TryLookFromMainViewport` does it the supported way. A secondary viewport's camera is the
exception: it genuinely follows nothing and has nothing to offset from, so unfollowing is right
there.

There is a second way to divide by zero here, and it is easier to hit: the controller crosses
`CameraRotation` with the reference frame's **+Z**, so a view pointing *along* that axis — the
local zenith under `Surface` — fails the same way. `docs/KSA-CAMERAS.md` has the full account of
this and every other controller.

## Commanding a vehicle's attitude from a mod hook

**Solvable by patching, like the post-processing entry above — recorded so the trade is a
deliberate one, and so the patch can be given up the day KSA offers a hook.**

**Wanted.** To point a vehicle from a mod.

**The engine reason.** `FlightComputer` is double-buffered across the frame, and every hook StarMap
offers lands on the wrong side of it. In `Program.OnFrame`:

```
2165   Universe.ApplyVehicleSolvers()          // vehicle.FlightComputer.CopyFrom(worker result)
2211   Universe.ExecuteNextVehicleSolvers()    // PrepareWorker snapshots vehicle.FlightComputer
2259   OnDrawUiViewports()                     // [StarMapBeforeGui] / [StarMapAfterGui]
```

A write made at 2259 is not in the snapshot taken at 2211, so the result applied at 2165 of the
*next* frame — computed from that snapshot — overwrites it. Then 2211 snapshots the overwritten
value. `[StarMapAfterOnFrame]` is later still. There is no hook between 2165 and 2211, which is the
only window where a write survives.

Confirmed in flight rather than inferred. A probe reading the flight computer either side of the
write reports, every frame without exception:

```
aimed=True dir=set | before Manual/None -> after Auto/Custom | error <0,0,0> rates <0,0,0>
```

The write lands and is gone by the next frame, and the engine's own error angles stay at zero
because it is not tracking anything. `FlightComputer.CopyFrom` does copy `AttitudeMode`,
`AttitudeTrackTarget` and `CustomAttitudeTarget`, so the round trip is not lossy — the write is
simply on the wrong side of it.


**What works instead.** A Harmony prefix on `Vehicle.PrepareWorker`, the one thing inside that
window a mod can reach. cairn5's
**PoweredGuidance** does the same, its own comment giving the same reason: the prefix runs "right
before the sim snapshots the flight computer". KSAGolf carries no such patch today.

**Why this is a much weaker thing than the ModMenu transpile above.** That one rewrites the IL of a
game method. This one patches a **`public virtual`** method — declared API, not an implementation
detail. A never-called method that references it puts it in `docs/KSA-API-SURFACE.md`, so a
signature change in KSA is a build error rather than a silent break. Harmony ships with StarMap, so
nothing is asked of a player. The one real cost is that a prefix runs inside the engine's frame
loop, where an exception is the game rather than a log line — so it must be wrapped, stand down on
the first failure, and say so once.

**Staging goes through the same window, for a different reason.** The worker that snapshot feeds is
running during the GUI pass and walks the live sequence list `ActivateNextSequence` rebuilds, so
staging from a hook races it; `docs/KSA-FRAME-ORDER.md` §1 has the engine code.

**What would unblock it properly**, and let the patch be deleted. Any hook between applying the
solver results and taking the next snapshot: a `[StarMapBeforeVehicleSolvers]` would do it, and so
would a public `Vehicle.SetAttitudeCommand(...)` writing into the pending snapshot rather than into
the live object.

## Custom part modules

**Wanted.** A mod's own behaviour on a part the engine updates — a body that is a real part-based
vehicle, integrated by the engine, rather than one the mod simulates itself.

**Why it is blocked.** Registering a custom module type means getting it into engine-internal
update lists, which is not reachable without Harmony patching the engine.

**Consequence.** A mod-owned body is simulated by the mod and drawn as a subpart whose transform is
written each frame. That is not purely a loss — it allows sub-frame stepping and cannot corrupt a
save — but it is a workaround, not a choice made freely.

---

## A trail behind a mod-simulated body

**Solvable by reflecting one private field — recorded so the trade is a deliberate one, and so it
can be given up the day KSA exposes an accessor.**

**Wanted.** A smoke or dust trail behind something the mod moves, through the game's own volumetric
trail renderer.

**The declarative route is closed.** The XML tag is real — `<PlumeTrail Id="DefaultPlumeTrail"/>`
inside a `<ReactionPlume>` — but the emitter only produces anything when
`current.State.DutyCycle > 0f && flag` (`KSA/KSA/Vehicle.cs:5678`), where `DutyCycle` is
accumulated by a **burning rocket core**. A mod-simulated body has no motor, no propellant and no
staging, and a real motor on a subpart would apply real thrust to the vehicle carrying it.

That gate is the `isActive` argument rather than a property of the renderer, so a caller passing its
own never meets it. `VolumetricTrailRenderer.SubmitEmitter` is `public` and `PlumeTrailEmitterState`
is a public class, so a mod can hold one cursor per body and submit it each frame — no nozzle, no
propellant, no thrust. The single obstacle is that `Program._volumetricTrailRenderer` is a private field with no accessor,
where its sibling exhaust renderer got one, so that one field is reflected and everything after it
is an ordinary public call.

The emitter follows a moved subpart correctly either way: its position comes from
`state.FxExhaustLocationVehicleAsmb = FxLocationAsmb.Transform(matrix)` where `matrix` is
`Parent.MatrixAsmb2VehicleAsmb` (`KSA/KSA/RocketNozzle.cs:280`) — exactly the matrix a mod writes
each frame when it moves a subpart.

**What would unblock it properly**, and let the reflection go. A public accessor for the trail
renderer, of the shape `Program.VolumetricExhaustRenderer` already has.

**Outstanding with RocketWerkz.** blackrack (KSA graphics programmer) suggested the XML tag.

## Configuring a part in the vehicle editor

**Wanted.** What a fuel tank gets: a section in the editor's part inspector with the part's own
settings, saved with the craft.

**Why it is blocked.** Three layers, and the third is the one that decides it.

- **The inspector has no extension point.** Its sections are written out longhand against concrete
  module types — `part.SubtreeModules.Get<Tank>()` then a Propellant block
  (`KSA/KSA/VehicleEditor.cs:6455-6458`), the same shape for decouplers and the rest. There is no
  registry, no per-module draw callback, nothing keyed on a mod.
- **A mod cannot register a module type** to be drawn for in the first place. See *Custom part
  modules* above.
- **A saved craft has nowhere to put it.** `PartTree` (`KSA/KSA/PartTree.cs:39-71`) is a fixed set
  of typed `ModuleStateful<…>.StateList` fields, one per module type the engine knows. It is the
  same closed shape as `UniverseData`, so per-part mod state cannot ride the vehicle file.

**What is not blocked, and why it is still not built.** The editor itself is readable:
`Program.Editor` is a public static (`KSA/KSA/Program.cs:227`) and `VehicleEditor.Selected`,
`Highlighted` and `EditingPart` are public `Part?` (`VehicleEditor.cs:549-555`). So the mod could
detect the editor, see the selected part and draw its own window beside KSA's.

It would be a window whose settings cannot be saved with the craft, which is worse than no window:
a choice made in the editor and silently gone on load is a bug report waiting to happen, and there
is no per-part key that survives the round trip.

**What would unblock it.** An extensible per-part blob in the vehicle save that survives a
round trip, or a registerable module type — either one makes the rest follow.

## Filtering the part picker by mod or manufacturer

**Wanted.** A way to narrow a shared category to one maker, so a player running several mods that
put parts in it can find one mod's parts among the rest.

The category half is already possible: `EditorTag` is a string-wrapping record struct rather than
an enum, `EditorTagDefinition.OnDataLoad` calls `VehicleEditor.RegisterTag`, and the picker draws a
row for every registered tag not flagged `NotaCategory`. Core's own
`Content/Core/CoreEditorTagsGameData.xml` says so in a comment addressed to modders. It is the
*filter within* a category that has nowhere to go.

**Why it is blocked.** Three things are missing and none has a workaround:

- **No manufacturer, vendor or author field** anywhere on `PartTemplate` or `PartGameData`
  (`KSA/KSA/PartTemplate.cs`). The only text a part carries is `DisplayName` and its `Id`.
- **No search box.** The picker's only input is a *diameter* combo, keyed on the selected tag
  (`KSA/KSA/VehicleEditor.cs:257`). The grid filter is the selected tag, whether the template is a
  subpart, and whether it is hidden — nothing that a mod or a maker could key on (`:316`).
- **The owning mod is known and never used.** `SerializedId.Mod` is set on every asset at load
  (`KSA/KSA/SerializedId.cs`), so the data exists — but `VehicleEditor` never reads it. The
  category state is a `private EditorTag _selectedTag` on the private nested `PartWindow` (`:52`),
  so a mod cannot drive the selection either.


The only mechanism available is *more tags*: a second `EditorTagDef` per maker, giving a row of its
own beside the shared one — which earns its place only once several mods share a category.

**What would unblock it.** A manufacturer or maker attribute on `PartGameData` that the picker
filters on, or a search box over `DisplayName`, or simply exposing the owning mod the engine is
already tracking.

## Where a structure's surface is

**Wanted.** Something landing on the launch pad to land on it rather than through it, and a cursor
over the pad to resolve to its top — so pointing at the pad's corner is not answered with the
ground beside it.

**Mostly unblocked in 2026.8.22.5348, and the mod has not taken it up.** A `LandmarkReference`
(`KSA/KSA/LandmarkReference.cs`) now carries a `StaticObjectId`, and `LocationReference` resolves it
through a public `GetStaticObject()` to a public `StaticObject` with declared `GroundOffset`,
`SurfaceHeight` and `FootprintRadius`, its own models, and a Bepu compound built from real
colliders. Core's `CoreLaunchPadA_Prefab_LaunchPadA` is 0.2 m + 1.5537 m over a 108.3 m circle, and
`Vehicle.GetInitialKinematicStateForLocation` stands a craft on exactly those numbers.

Terrain itself was never the problem — `Celestial.GetTerrainHeightFromDirCcf` is public and
accurate, and the cursor already uses it. Earth's height field is also decal-levelled to a fixed
altitude within 275 m of each launch site, so the ground the pad stands on is flat before the pad
is added.

**What is left.** The footprint is a *radius*, so a square pad's corner still answers with the
ground beside it, and the height it gives is one number for the whole disc rather than the surface
under a particular point. `KsaWorld` resolves the cursor against terrain alone, because a pad
modelled as a thicker planet swung a bearing measured from a craft beside it through
168° between two adjacent pixels at the pad's edge.

**What would unblock the rest.** A raycast against static geometry. The pieces exist —
`StaticObject.CollisionShape` is a public `TypedIndex` and `ConstraintSim.UnlockShapes()` hands out
the Bepu `Shapes` registry — but nothing here has tried it, and the engine's own per-triangle path
is `Part.RayCastEgo` against `Ray.RaycastWatertight` (`KSA/KSA/Part.cs:2534`, `:2597`), which takes
a `Part` and not a landmark.

## Drawing a shape the gizmo renderer does not have

**Wanted.** Any shape worth looking at drawn at a world position — a solid ring on the ground, a
marker, a volume, a footprint — without attaching it to a vehicle.

**Why it is blocked.** `GizmosRenderer` draws two things. `Render` is `RenderSpheres` then
`RenderLines`, and `GizmoType` has exactly `Sphere`, `Line`, `Num`. Everything else it offers is
built from those: `DrawCircle` is twelve line segments, `DrawWireBox` and `DrawCylinderSides` are
line loops. There is no filled polygon in it at all.

The engine can *generate* the geometry — `ProcGenMeshLibrary.GenerateTorus` writes positions,
indices, normals and UVs, alongside sphere, cube and plane generators. What it cannot do from a mod
is *draw* it: arbitrary geometry has to be uploaded as a `SimpleVkMesh` and submitted inside a
render pass, and **no StarMap hook carries a command buffer** — its seven method attributes are
plain prefixes and postfixes on methods that take none. Patching a private render method with
Harmony would work and is not worth it: a private method sits outside the API surface
`ksa-api-diff.sh` checks, so it breaks silently on a KSA update that passes every other gate, and
an exception inside the render pass takes the game down with nothing pointing at the mod.


**What is possible today, and what it costs.** Real geometry reaches the screen through the asset
pipeline: a mesh in a mod's atlas, declared as a `<SubPart>`, positioned by writing its transform
each frame, which holds measured to 79.5 km from the part carrying it. So a real torus is available, at the cost of art, XML, transform code, and living on a
vehicle's part tree, which is awkward for a marker that should exist whether or not a craft does.

Meanwhile `KsaWorld.DrawTorusEcl` rings solid spheres closely enough to read as a tube, and drapes
each one onto the terrain under it.

**What would unblock it.** A gizmo primitive with a filled surface, or any hook that hands a mod a
command buffer.

---

## `IsValid()` on a distance is an astronomical-scale check

**This one is not a missing feature — it is a predicate that does not mean what it is named**,
which is worse, because the code reads correctly and is silently wrong.

**Wanted.** To ask KSA whether a body's atmosphere and ocean are usable, with KSA's own
`IsValid()`, rather than second-guessing it.

**The engine reason.**

```csharp
// KSA.DistanceReference
public override bool IsValid()
{
    return !double.IsNaN(_value) && Math.Abs(_value) > 100000.0;
}
```

A distance is "valid" only above **100 km**. That is a sane check for the orbital distances the
type mostly carries and nonsense for anything planetary-surface sized — and two of the references
a mod needs are exactly that:

| Reference | Earth's value | `IsValid()` |
| --- | --- | --- |
| `PhysicalAtmosphereReference.ScaleHeight` | 8 km | **false** |
| `OceanReference.Level` | 0 m | **false** |
| `OceanReference.TransparencyDepth` | 100 m | **false** |

Both composites fold those in — `PhysicalAtmosphereReference.IsValid()` is
`ScaleHeight.IsValid() && SeaLevelDensity.IsValid() && SeaLevelPressure.IsValid()`, and the
ocean's is the same shape — so **`air.IsValid()` is false for every realistic atmosphere and
`sea.IsValid()` is false wherever there is water.** `DensityReference` and `PressureReference` are
fine; they test `_value > 0`. It is the distance that is wrong.


**What it costs.** Gated on `air.IsValid()`, a mod reads **vacuum at ground level, on Earth,
always** — no drag, ever. Confirmed in flight: a falling body at 81 m, over an
atmosphere reporting a correct 1.225 kg/m3 sea level, an 8 km scale height and a 167 km top — with
`valid=False`. A ground test gated on the ocean's `IsValid()` has `hasSea` always false, and a body
falls through the waterline to the seabed.

**The workaround.** Do not call `IsValid()`. Check the terms actually divided by
— `SeaLevelDensity > 0` and `ScaleHeight.InMeters() > 0` — and use a **null** ocean reference as
the discriminator for a body with no water, which is what `Astronomical.GetOceanReference`
returning `BodyTemplate.OceanReference` already means. `KsaWorld.MediumDensityRatioAt` and
`Ksa/GroundTest.cs` both do.

**What would unblock it.** `DistanceReference.IsValid()` dropping the 100 km floor, or the
atmosphere and ocean composites testing their own fields directly rather than delegating a
surface-scale distance to an astronomical-scale predicate.
