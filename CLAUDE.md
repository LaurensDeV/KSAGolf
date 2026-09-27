# CLAUDE.md

A golf mod for **Kitten Space Agency** (KSA, RocketWerkz): clubs a kitten holds, a ball, and later
golf carts and minigolf courses.

What exists is the foundation: the frame and epoch plumbing, ground and hull contact, air and water
drag, cursor picking, the camera controller, the kitten pose and attachment probes, the developer
bridge, and the build, CI and KSA-update tooling. **Putting is the one golf feature**: the flown kitten
carries a putter, drops a ball, steps up to it with G and swings with the mouse (`Ksa/Golf.cs`), in
third person or through its eyes with V.
[Where golf starts](#where-golf-starts) says what the rest builds on.

## Read this first

**`docs/FRAMES-AND-EPOCHS.md` is the one to read before touching anything that moves, draws or
times.** A frame or epoch mismatch is multiplied by 29.8 km/s of ecliptic motion, and that file has
the engine's actual contract, the rules that follow from it, and how to tell the failure shapes apart.

**`docs/KSA-MODDING-NOTES.md` is the distilled result of reverse-engineering the game.** It has the
runtime, the loader contract, the type signatures, the reference frames and the gotchas. Read it
before touching anything KSA-facing.

KSA has **no official code-modding API**. Everything is community tooling against a pre-release
game, so the API moves between builds.

## Comments and documentation

**Docs are part of the change, not a follow-up.** If a change makes a line in `CLAUDE.md`, a
`docs/` file, `README.md` or a comment untrue, fix it in the same commit. A stale line is worse than
a missing one: it is trusted, and nothing in a build fails when it goes wrong.

**Comment why, never what.** The code says what it does. A comment earns its place only when the
reason is not recoverable from reading it — an engine contract, a measured number, a constraint
imposed from somewhere else in the frame, an ordering that looks arbitrary and is not.

```csharp
// Anchor to the platform sample from the same frame: those differ by metres on a landed craft.  ok
double3 offset = positionEcl - platformEcl;

// Subtract the platform position.                                                            delete
double3 offset = positionEcl - platformEcl;
```

**Keep them short.** A sentence or two. If a comment needs paragraphs, the explanation belongs in
`docs/` with a one-line pointer to it.

**State the fact, not the history.** A comment says what is true now. It does not narrate what the
code used to do, what broke, when it was reported, or which commit fixed it — that belongs in git,
and the reasoning belongs in `docs/`.

**When in doubt, delete.** An unnecessary comment is another thing that can drift out of step with
the code and mislead the next reader.

## Committing

**Every commit message must be a [Conventional Commit](https://www.conventionalcommits.org/).**
semantic-release parses these to decide the next version, so a message that does not parse silently
produces no release and never appears in the changelog.

```
feat(ball): roll the ball down the slope of the green
fix(club): seat the grip on the wrist it was measured on
docs: write an install guide
refactor(sim): split the lie out of the ball's flight
```

| Type | Version effect |
| --- | --- |
| `feat`, `fix`, `perf`, `build`, `revert` | **patch** |
| `!` after the type, or a `BREAKING CHANGE:` footer | **major** |
| `docs`, `refactor`, `test`, `chore`, `ci`, `style` | no release, and none appear in the changelog |
| a **minor** | never automatic — tag it by hand |

**The type says what a change is; it does not decide how big the version bump is.** `feat` cuts a
patch like everything else, so labelling something a feature is a changelog decision. A minor is a
deliberate act for a milestone — a new playable feature set, or a KSA compatibility milestone:

```bash
git tag -a v0.2.0 -m "the first hole"
git push origin v0.2.0
```

semantic-release reads the newest tag and carries on from it, so the next `fix` after that is
0.2.1. A tag is the only thing that anchors a version.

**`feat` means a player can observe the difference in the shipped archive.** Capability nothing
reachable uses yet is `refactor`; it becomes a feature in the commit that uses it. Developer tooling
is `chore`, `ci`, `test` or `refactor`, whatever its scope — `feat(tools)` still cuts a release. The
commit-msg hook warns when a `feat`/`fix`/`perf` commit touches nothing under `src/KSAGolf/`.

**No `Co-Authored-By` trailer, and no other attribution footer.** Commits carry the repository
owner's name and nothing else, whoever or whatever drafted them.

Split unrelated work into separate commits. **Batch a session's `docs` into one commit** — a
night's measurements, the doc they land in and the stale lines they falsify are one unit of work.
An `arm/*` or `agent/*` branch that loses does not merge; one that wins is squashed into one
`feat`/`fix`.

**Commit to `dev`, not to `main`.** `main` is the release branch: a push to it cuts a release.

**Do not commit a behaviour fix as a fix until it has been verified in game.** Compiling, passing
the suite and having a plausible mechanism are not evidence — the hardest bugs live in the gap
between the maths and what KSA actually does. Ship the diagnostic, not the guess, and say in the
message what is unverified.

**A regression test only counts if it fails against the old code.** Check that it does, every time.

**This is enforced.** `tools/check-commit-msg.sh` runs both as a local `commit-msg` hook
(`./tools/install-hooks.sh`, using `core.hooksPath`) and as a CI job over every commit in a push or
PR. It skips merges, reverts, `fixup!`/`squash!` and semantic-release's own `chore(release):`.

## Environment

- **KSA install**: `/mnt/c/Program Files/Kitten Space Agency` (Windows game, WSL dev)
- **KSA build these notes were taken against**: `2026.9.22.5482`
- The system `dotnet` is 8.0 and **cannot build this** — the mod targets **net10.0**. A .NET 10 SDK
  is installed at `~/.dotnet`. **Use `tools/build.sh` / `tools/test.sh`**, which source
  `tools/env.sh`. In an interactive shell, `source tools/env.sh` once.
- `Import/` holds the game's assemblies and is **gitignored**. Repopulate with
  `./tools/sync-import.sh`. The build also finds a game install or a `ksa-game-assemblies` checkout
  on its own, see `Directory.Build.props`.
- **When KSA updates, four things have to move together**, not just `Import/`. See
  [After a KSA update](#after-a-ksa-update).
- **The game is launchable from WSL**: `tools/run.sh` starts `StarMap.exe` directly, found under the
  Windows user profile — override with `STARMAP_DIR`.
- **A developer's install is marked by a `developer` file beside the DLL**, which `tools/deploy.sh`
  writes and `tools/package.sh` never carries. `Build.Developer` reads it at load, and only then
  does the bridge start and the panel show Capture for Claude. **A new developer-only control goes
  behind it**, and the startup line in the log says which kind of install is running.
- **The mod writes its own log** to `<KSA user dir>/Logs/KSAGolf.log`, with the session before kept
  as `KSAGolf.prev.log`; `./tools/ksa-user-dir.sh` prints that directory and `./tools/run.sh --attach`
  follows the log. KSA's own log, the newest `KittenSpaceAgency.<yymmdd-hhmmss>.<pid>.log` in the
  same folder, is still the place to look for mod discovery and asset/XML errors.

## Commands

```bash
./tools/doctor.sh                          # can this machine build, test and run it? -- start here
./tools/check-all.sh                       # everything CI runs; also the pre-push hook
./tools/build.sh                           # build the mod (handles the SDK PATH)
./tools/test.sh                            # the headless tests over Sim/; needs the assemblies, not the game
./tools/validate-parts.py                  # asset XML ids and paths; runs in deploy.sh
./tools/check-boundary.sh                  # Sim/ must not reference KSA types
./tools/check-comments.sh                  # history in comments, XML docs on privates, ratios
./tools/check-docs.sh                      # layout table, API counts and KSA build vs reality
./tools/package.sh                         # release zip into dist/ -- no symbols, no game DLLs
./tools/deploy.sh                          # build and install into the KSA mods folder
./tools/run.sh                             # build, deploy, launch, show the mod's output
./tools/run.sh --attach                    # follow a game that's already running
python3 tools/ksa-mcp/server.py cli status # drive a running game through the bridge -- docs/VISUAL-TESTING.md
./tools/model/checkmesh.py <atlas.glb>     # z-fighting and degenerate UVs in an exported mesh
./tools/ksa-user-dir.sh                    # where KSA keeps Logs/, mods/ and saves on this box
./tools/setup-starmap.sh                   # one-off: install StarMap and write its config
./tools/check-assemblies.sh --game         # has the installed game moved past the lock?
./tools/check-ksa-version.sh               # has RocketWerkz published a newer build?
./tools/api-surface.sh                     # record the KSA API this mod binds to
./tools/api-surface.sh --check             # ...and fail if the record is stale
./tools/decompile-assemblies.sh ../ksa-game-assemblies   # refresh the decompiled corpus
./tools/ksa-api-diff.sh ../ksa-game-assemblies           # which KSA changes hit this mod?

source tools/env.sh                        # then bare dotnet works in this shell
cd tools/apidump && dotnet run -- ../../Import members KSA.Vehicle   # inspect the game API
./tools/meshinfo.py "<KSA>/Content/Core/Meshes/CoreStructuralA_MeshAtlas.glb" Tube  # mesh bounds
```

## Layout

**The source is split by whether it touches KSA.** `Sim/` cannot; `Ksa/` does. The test project
links `Sim/**` wholesale and references no KSA assembly, so a `using KSA;` under `Sim/` fails the
test build, and a new file under `Sim/` is tested the moment it exists.

| Path | What |
| --- | --- |
| **`src/KSAGolf/Sim/`** | **no KSA types, linked into the tests wholesale** |
| `Sim/Config.cs` | session-wide settings — what cannot differ between two things in one world |
| `Sim/Vec.cs`, `Sim/DrawAnchor.cs` | vector helpers, and the two-instant draw anchor |
| `Sim/StepGate.cs` | hands a simulation step out once and only once |
| `Sim/FrameLatch.cs` | hands a **frame's** work to whichever hook reaches it first — the UI pass is skipped while the UI is hidden and the frame postfix is not |
| `Sim/SimClock.cs` | classifies a step: usable, paused, or too long to integrate |
| `Sim/SmoothedStep.cs` | the step evened out, for the one consumer that wants a smooth clock — a camera's ease, never the world |
| `Sim/Medium.cs` | what air or water does to a body — buoyancy and quadratic drag against **one reference air for every body**, and a sphere's drag constant from its mass, diameter and coefficient |
| `Sim/ContactSweep.cs` | whether a body runs into something over one step, rather than whether it is near at either end |
| `Sim/GolfBall.cs` | **the ball, in the body-fixed frame of the world under it** — flight, bounce, rolling resistance, slopes and coming to rest |
| `Sim/PutterRig.cs` | where the putter and the ball sit around a kitten at address, in its model space, for a swing angle |
| `Sim/PuttingStroke.cs` | the mouse's left and right swinging the club, and the face crossing the ball as the strike |
| `Sim/Orient.cs` | the rotation carrying one facing-and-up pair onto another |
| `Sim/StrikeSound.cs` | which recorded whack a putt or a landing sounds like, and how loud |
| `Sim/HeadLook.cs` | looking around from the kitten's eyes with the captured mouse, measured from the world |
| `Sim/IMouseDrag.cs` | a view the right button turns: `ChaseOrbit` and `HeadLook` |
| `Sim/Stance.cs` | where a kitten is drawn standing to address a ball — **drawn, because a landed kitten cannot be moved** |
| `Sim/CourseGrid.cs` | a course's grid: cells, sides, orientations, and **a port as the cell edge a lane crosses**, never a tile |
| `Sim/PieceCatalogue.cs` | the pieces a course is built from, their ports and the tiles they take up — **not always a rectangle**, so a bend leaves its corner free — **ids append-only**, a saved course names them |
| `Sim/Course.cs` | **one hole**: pieces joined port to port from tee to cup, the check that says why it cannot be played, and a ghost snapping onto an open port |
| `Sim/CourseEdits.cs` | undo and redo over a course being built |
| `Sim/PieceShape.cs` | **what each piece is exactly made of** — rails, posts, cup and felt — the one source the ball collides with and the Blender models are built from |
| `Sim/CourseSurface.cs` | a laid course as the ball meets it: the felt as ground, rails and posts **swept**, and the ground beyond it |
| `Sim/CourseColliders.cs` | the slab under each piece as a box, for the kitten to stand on |
| `Sim/CourseLines.cs` | a course as lines on its grid, with a ramp's rise, and the sample hole of every piece |
| `Sim/CourseBuild.cs` | the builder's straight-down camera, the cell under the cursor, and the ghost a click would place |
| `Sim/CourseFile.cs` | a course as saved, its floor kept **above the ground** rather than as a radius, and the provisional sizes it is built to |
| `Sim/IHullTest.cs` | **the seam a moving body asks whether it truly touched a craft** |
| `Sim/IGroundTest.cs` | where the ground is under a point |
| `Sim/CoarseGroundTest.cs` | a ground test that skips the lookups a body high above the ground cannot need |
| `Sim/ITerrainHeights.cs` | **the seam anything looks over the real skyline through** |
| `Sim/TerrainMask.cs` | whether a ridge hides one point from another, and how few samples that can cost |
| `Sim/TerrainRay.cs` | where the pointer meets the ground — **the first place the ray goes under it**, walked from the eye |
| `Sim/TerrainMap.cs` | a local east/north frame on a body, and the square of ground around it |
| `Sim/GroundSlope.cs` | how steep the ground is at a point — the **gradient**, never a ring's median, because a ring halves a planar slope |
| `Sim/LineOfSight.cs` | whether a body is between the viewer and something |
| `Sim/Picking.cs` | what the cursor's ray meets, and what is nearest it on screen |
| `Sim/CursorAim.cs` | cursor to viewport coordinates — **offset and scale**, because a render scale makes them different sizes |
| `Sim/ChaseView.cs` | where a camera riding behind something in flight stands, how it is orbited, and how it eases onto it |
| `Sim/ChaseOrbit.cs` | the player looking around a chased body with KSA's own orbit drag and wheel |
| `Sim/StableUp.cs` | a camera's roll carried from frame to frame, so a view whose up passes along its line of sight turns rather than flips |
| `Sim/ViewClaim.cs` | who may hold the player's main view, and what that means for the loser |
| `Sim/Distance.cs` | a distance in the unit that shows it |
| `Sim/BridgeCommand.cs` | one command dropped into the bridge's folder, read — **text in**, so every refusal is testable here |
| **`src/KSAGolf/Ksa/`** | **everything that binds to the game** |
| `Ksa/KSAGolfMod.cs` | StarMap entry point and frame hooks |
| `Ksa/KsaWorld.cs` | most KSA contact is funnelled here — keep it that way |
| `Ksa/PreRenderHook.cs` | **one of the places this mod patches the game**, listed under the design decisions — a step before the render on a frame that draws no UI |
| `Ksa/WorldReloadHook.cs` | another — **that a save was loaded**, which nothing else can tell |
| `Ksa/FirstPersonHook.cs` | a patch — **first person strips the kitten from the main view only**, keeps its whole shadow, and holds its facing on the look; KSArmory's technique |
| `Ksa/ViewDrawHook.cs` | a patch — **where a mesh of the mod's own is drawn into every viewport**, the ball; the only moment a static mesh's draw is not cleared before the render |
| `Ksa/Golf.cs` | **putting**: the flown kitten carries the putter, drops a ball, steps up to it on G, and the mouse swings the club |
| `Ksa/CourseBuilder.cs` | **building a hole from above**: the view straight down, a ghost snapping onto open ports, place, move, remove, undo, save — **the view takes the keys and clicks from the game** so the kitten does not walk off |
| `Ksa/CourseCollider.cs` | a patch — **the course as ground a kitten stands on**: static boxes in each physics bubble, **registered as clutter**, the only kind of mod static the engine lets collide |
| `Ksa/GolfPose.cs` | the kitten bent over the ball with its paws on the grip, and the club placed against each view's own wrist |
| `Ksa/GolfSounds.cs` | a sound from `KSAGolfSounds.xml` played at a point in the world |
| `Ksa/PadSurface.cs` | **the top of a launch site under a point** — a ray cast down into its collider and the ground it draws |
| `Ksa/KittenFrame.cs` | a kitten's model space against the world, and standing a kitten on a spot facing a chosen way |
| `Ksa/LevelHorizonController.cs` | KSA's fixed camera controller, with an up vector it does not otherwise offer, and the orbit drag while a view is borrowed |
| `Ksa/SceneCamera.cs` | a camera held for a composed shot, from the bridge's `frame` |
| `Ksa/GroundTest.cs` | the surface under a point, off the engine's own height field |
| `Ksa/HullTest.cs` | whether a step meets a craft's actual geometry, per triangle |
| `Ksa/TerrainHeights.cs` | one body's height field, sampled coarsely and many times |
| `Ksa/CraftMover.cs` | picks a craft up and sets it down elsewhere, from the panel |
| `Ksa/AttachmentMesh.cs` | a mesh of this mod's own, built to hang on a kitten — **the material slot filled before the renderable is made** |
| `Ksa/KittenPoseProbe.cs` | **whether a mod can pose a kitten** — bones posed through the engine's own pose-processor list, from the bridge's `kitten_pose` |
| `Ksa/KittenPropProbe.cs` | **whether a mod can put something in a kitten's hand** — a mesh on the avatar's cosmetic attachments, from the bridge's `kitten_prop` |
| `Ksa/Bridge.cs` | **commands from outside the game**, read from a folder beside the log and answered in another — pause, step, frame a shot, capture, pose a kitten. **Files, not a socket** |
| `Ksa/Build.cs` | what build this is, read off the assembly — and **whether it is a developer's install** |
| `Ksa/Log.cs` | the mod's own log file, which is the only debugging channel it has |
| `Ksa/Ui/Ui.cs` | the panel: world clock, craft mover, send to another body, logging, Capture for Claude |
| `Ksa/Ui/ModMenuEntry.cs` | a copied attribute so ModMenu can list this mod — **wanted gone**, see `docs/BLOCKED-ON-KSA.md` |
| `src/KSAGolf/mod.toml` | serves as both the content-mod and StarMap manifest; `assets` lists every asset XML by name |
| `src/KSAGolf/KSAGolfSounds.xml` | the putt and landing sounds, `Sounds/` beside it — cut from a recording the user supplied |
| `src/KSAGolf/KSAGolfCourse.xml` | every course piece's model, one `<GltfFile>` each in metres with the footprint's south-west corner at the origin, a mirrored twin for a chiral piece, a plinth for raised ones, and **one material over one atlas for all of them** |
| `src/KSAGolf/KSAGolfProps.xml` | the ball and the putter, as `<GltfFile>` and `<PbrMaterial>` pairs a kitten holds — `Meshes/` and `Textures/` beside it; the `.blend` source is outside the repository |
| `tests/KSAGolf.Tests/` | links the KSA-free sources and runs them headlessly |
| `KSAGolf.sln` | both projects, for editors only — every script builds a csproj directly |
| `tools/apidump/` | reflection dumper for the game assemblies |
| `tools/apisurface/` | reads the KSA API this mod binds to out of its own metadata |
| `tools/validate-parts.py` | asset Ids, texture paths, mesh Ids against the atlas, stacking connectors sized, planes shared between subparts |
| `tools/meshinfo.py` | prints mesh bounds from a KSA `.glb` atlas |
| `tools/model/` | the checkers over exported meshes, and the preview renderer — see `tools/model/README.md` |
| `tools/ksa-mcp/server.py` | **an MCP server over the bridge**, registered in `.mcp.json`, returning captures inline |
| `tools/vis/vis.py` | what the bridge's pictures are judged with: crops, same-instant diffs, the temporal-noise map, contact sheets |
| `tools/screenshot.sh` | captures the Windows screen; readable from here |
| `tools/spacedock-changelog.py` | a release's notes cut to what SpaceDock accepts — **10,000 characters** |
| `docs/FRAMES-AND-EPOCHS.md` | **the engine's frame and epoch contract**, and the failure shapes that break it |
| `docs/KSA-MODDING-NOTES.md` | the runtime, the loader contract, type signatures, frames and gotchas |
| `docs/KSA-FRAME-ORDER.md` | **the engine's own frame order and what instant each sample belongs to** |
| `docs/KSA-CAMERAS.md` | what the engine does with cameras and viewports |
| `docs/KSA-TERRAIN.md` | **where the engine thinks the ground is** |
| `docs/KSA-API-SURFACE.md` | **generated** — the members an upgrade has to preserve |
| `docs/BLOCKED-ON-KSA.md` | **what a mod cannot build**, with the engine reason and what would unblock it |
| `docs/KITTEN-ARMS.md` | **a record** — posing a kitten, hanging a mesh on it, and what the rig is like to hold things with |
| `docs/FROM-KSP-MODDING.md` | the concept map for anyone arriving from KSP part modding |
| `docs/VISUAL-TESTING.md` | how an agent sees what the mod draws, through the bridge |
| `.claude/skills/upgrade-ksa/` | the whole KSA-update procedure, as a skill |
| `.claude/skills/ksa-blender/` | authoring a part in Blender over MCP, and the export contract KSA enforces |

## Where golf starts

- **Putting** is built: `Ksa/Golf.cs` holds the session, `Ksa/GolfPose.cs` the kitten and club,
  `Sim/GolfBall.cs` the ball, `Sim/PutterRig.cs` and `Sim/PuttingStroke.cs` the swing. The bridge's
  `golf` command drives it without a hand on the mouse.
- **The ball lives in the body-fixed frame of the world under it**, so it carries none of the
  planet's motion and the frame contract reduces to drawing it against the body's pose at the instant
  it is drawn. It rests on the higher of the height field and a launch site's collider and drawn
  ground, found by a ray cast down into them (`Ksa/PadSurface.cs`): the field knows only the terrain,
  the apron stands 10 cm proud of it, and the drawn road runs on 40 m past the collider.
- **A kitten is stepped up to the ball by drawing it there**, not by moving it: a landed kitten's
  `Teleport` rebuilds it from an orbit and throws it about. `Sim/Stance.cs` is the drawn step.
- **Longer shots** want the ball to leave the ground: `Sim/Medium.cs` already drags it, and
  `Sim/ContactSweep.cs`, `Ksa/GroundTest.cs` and `Ksa/HullTest.cs` are its contacts with craft.
- **Watching the ball.** `Sim/ChaseView.cs`, `Sim/ChaseOrbit.cs`, `Sim/ViewClaim.cs` and
  `Ksa/LevelHorizonController.cs` are a camera riding a body with the main view.
- **Aiming a shot, and a preview of it.** A preview is best flown — the same body stepped through
  the same air and ground — rather than solved in closed form, which drag rules out.
- **Minigolf courses** are one hole each, pieces on a grid joined port to port from a tee to a cup.
  `Sim/Course.cs` and its neighbours are the grid model, and `Ksa/CourseBuilder.cs` lays one out from
  above (B). The ball banks off its rails and drops in its cup through `Sim/CourseSurface.cs`, off
  exact shapes rather than the meshes' triangles, which snag at every seam; a kitten stands on its
  slabs through `Ksa/CourseCollider.cs`.
- **Carts and course pieces** are parts: see [Parts](#parts), `docs/FROM-KSP-MODDING.md` and the
  `ksa-blender` skill. A craft is placed with `Ksa/CraftMover.cs`; `Sim/TerrainMap.cs` and
  `Sim/GroundSlope.cs` are a local frame and the lie of the ground.

## Parts

**New art is authored in Blender over MCP, in a live session** — `.claude/skills/ksa-blender/SKILL.md`
is the whole procedure. **A human signs off on the geometry before anything is unwrapped, baked or
exported**, because everything past that point is welded to the shape. An authored asset's `.blend`
is its source and is not in this repository, which is why `checkmesh.py` and `validate-parts.py` are
the only gate on something nobody can rebuild.

**Every face needs UV area, and no two faces may share a plane.** A zero UV derivative gives a
zero-length tangent and NaN shading; coplanar faces z-fight. Both look identical in game (flickering
white speckle), so diagnose with `tools/model/checkmesh.py`, not by eye. Two *bodies* sharing a plane
are invisible to it, since it reads one mesh at a time; `validate-parts.py` places them as the XML
does and checks the pairs.

**The asset XML sits at the mod root.** `<MeshAtlas Path="Meshes/…">` is relative, and it is not
documented whether it resolves against the mod root or the XML's own directory; at the root those are
the same. Every asset XML must be listed in `mod.toml`'s `assets`, or it is silently never loaded.
Textures are PNG. Run `./tools/validate-parts.py` after touching any of it: a bad Id or path is a
*silent* in-game failure.

**A shipped part's subpart list is append-only.** KSA pairs a saved part with its current definition
positionally, so removing a `<SubPart>` throws from inside `Popup.DrawAll` and terminates the game on
every save holding that part. **Nor is a shipped `<Part>` Id renameable** — a save names it, and
loading throws `PartTemplate is null`. Treat subpart template Ids the same way.

**A stacking connector carries its diameter as `<Scale>`.** An unsized node is size one, so a 3 m
part sinks onto a Core tank's nested `Internal` node — 24.5 cm into a 3 m tank — and nothing reports
it. `validate-parts.py` fails an unsized one.

**A part that surface-attaches cannot start a craft.** `IsAllowedAsRootPart` rejects a part if any of
its connectors is `ToSurface` or `FromSurface`, so a vehicle roots and an accessory rides. Core's
`Radial` tag stops anything being mounted *on* the part carrying it. Nothing logs either: the part is
simply greyed out. `docs/KSA-MODDING-NOTES.md` has the gates.

**Writing a subpart's transform each frame moves it**, which is how a part articulates while staying
one part in the editor. `ResetCachedPosMatrixValues()` must follow the write or it is stored and
ignored; a subpart rotates about its own mesh origin, so export it recentred on its pivot and put it
back with `<Position>`; and subparts do not nest — every `<SubPart>` is placed against the `<Part>`,
so a chain of moving bodies is composed by the mod.

**The part is inert; the behaviour is in C#.** KSA sees structure with mass and a collider; the mod
finds the part on a vehicle and does the rest. Registering a custom module type into the engine's
internal update lists is not reachable without patching.

## The panel

**A control lives with what owns it.** Session settings — the world clock, what is drawn, logging —
are on the panel or in its settings; anything that describes one part of one craft belongs with that
part. The test is whether two things in one world could sensibly disagree.

**One field, one control.** Two controls for one field, under two names, move each other silently.

**A control that opens a window is a button, never a tick box.** Tick boxes are for state.

**An explanation is a tooltip; a line under a control is state the player has to act on.** What a
control does goes in `Tip()`; what stays inline is a live readout, a warning about what the setting
costs, or the answer to "why is nothing happening", which a tooltip hides as surely as a fold does.

**The floating button stays on whatever else offers a way back.** The menu-bar entry appends to KSA's
bar through ImGui behaviour rather than a supported hook, and the setting that would bring the button
back lives inside the panel it reopens.

## Design decisions worth not re-litigating

**Everything is computed in the ecliptic (`Ecl`) frame** and converted to camera-relative `Ego` only
at draw time. `Ego` is a pure translation of `Ecl`, so this is exact for directions — but `Ego`
belongs to the camera that sampled it, so a *position* taken against one camera is wrong for another.

**`Sim/` must stay free of KSA types**, and this enforces itself. When something KSA-facing turns out
to have testable maths inside it, move the maths into `Sim/`.

**And a `Sim/` entry point differences its own inputs.** It takes both frame-carrying terms —
`(shooterPos, shooterVel, targetPos, targetVel)` — never a relative velocity computed in `Ksa/`, which
moves the subtraction carrying the whole frame contract to a call site no test reaches. Test it for
*invariance*: add the same velocity to both inputs and assert the answer does not move.

**Anything the mod simulates runs on simulated time, never on player time.** StarMap's frame hook
hands over a player-time delta, which keeps running while the game is **paused** and ignores
**timewarp**. `KsaWorld.ConsumeSimStep()` is the clock: the span between step boundaries, classified
by `Sim/SimClock.cs`. A camera move that tracks a moving body runs on it too, so it holds still
through a pause; viewing durations run on the player's clock and stop in a pause.

**A frame the hook never runs is integrated on the next one.** `Universe.GetLastSimStep` reports only
the latest step, so `ConsumeSimStep` hands out the span between step boundaries instead, and a skipped
frame is closed on the next rather than lost — at 29.8 km/s, lost time lands in the drawn offset.

**The mod is stepped from two hooks, because KSA guards one of them.** `Program.OnFrame` wraps the
whole UI pass in `if (DrawUI)`, and `DrawUI` is F2. `KSAGolfMod.StepOnce` runs from whichever hook
reaches it first, latched by `Sim/FrameLatch.cs`: the GUI pass is preferred because stepping there
pairs a drawn offset with its anchor's epoch, and the frame postfix is the fallback. **The release
must be unconditional** — a latch left set stops the mod for the session. Driving a borrowed camera
is not drawing, so it goes with the step rather than with the panel.

**Something the mod draws against a craft is offset by `PositionEcl − platformEcl`, measured after
the step against the platform sample from the same frame, with no extrapolation.** Both then advance
in lockstep and the offset changes by exactly the body's own motion. Measuring before the step, or
re-projecting the platform by a `dt`, both leak `v·Δdt`: a 1 ms wobble in the step is 30 m at
29.8 km/s, and a simulation-speed change swings the step by ~17 ms. Tests must advance the platform
*before* the update, and vary the step — a constant step cannot tell the right phase from the wrong.
`docs/FRAMES-AND-EPOCHS.md` is the whole account.

**The draw anchor uses two different instants on purpose.** `DrawAnchor.Ego` is sampled this frame;
`DrawAnchor.Ecl` is the platform position the geometry was measured against. Collapsing them puts
the whole overlay beside the craft. `DrawAnchorTests` fails if they are collapsed.

**A fast body is swept, not sampled.** A 70 m/s drive moves over a metre a frame at 60 fps against a
ball 43 mm across, so a distance measured at the ends of a step says nothing about the middle.
`Sim/ContactSweep.cs` sweeps the segment. **The sphere only rejects; the hull decides**: a craft's
`MeanRadius` stands metres clear of its skin, so it is the broad phase, and `IHullTest` casts against
the actual triangles. **A hull test that cannot answer falls back to the sphere, never to "no hit"**,
or a craft the engine will not resolve becomes silently untouchable.

**The patches are prefixes and postfixes whose absence degrades.** `Ksa/PreRenderHook.cs` prefixes
`Program.OnFrameCelestials`, a private method, so a rename makes the patch not apply; it is logged
once and the frame postfix steps as before. `Ksa/WorldReloadHook.cs` postfixes `Program.OnGameLoaded`,
public and static, so its signature is pinned in `docs/KSA-API-SURFACE.md` and a change to it is a
build error; losing it costs one reload's worth of stale state. `Ksa/ViewDrawHook.cs` postfixes
`SuperMeshRenderSystem.ClearBuckets`, public and pinned the same way; losing it leaves the ball
undrawn and nothing else. `Ksa/FirstPersonHook.cs` prefixes and postfixes `KittenEva.UpdateRenderData`,
`AnimatedRenderable.Draw` and `StaticMeshRenderable.Draw`; losing it leaves first person looking out
through the kitten's head. `Ksa/PadSurface.cs` prefixes `Universe.ExecuteNextVehicleSolvers` to copy
the launch sites' colliders; losing it rests the ball on terrain alone. `Ksa/CourseCollider.cs` prefixes
the same method to make shapes, and `ConstraintSim.DetectCollisions`, `Simulate` and `TryResetForPool`
to keep the course's statics in each bubble, and reaches a private dictionary of the engine's ground
clutter by reflection; losing it lets a kitten walk through a course. **Nothing in a patch may throw** — it
runs inside the engine's frame loop. Harmony ships with StarMap, so a player installs nothing.

**The world is never left across a save load.** StarMap has no load hook and the flight scene
survives a load, so `WorldReloadHook` is the only signal; `KSAGolfMod.ForgetTheWorld` drops what
belonged to the old world. **Losing the craft being flown is not leaving flight** either:
`Universe.DestroyVehicle` clears `Program.ControlledVehicle` and the scene carries on, so gate on
`KsaWorld.InFlightScene`, not `InFlight`, for anything that must survive it.

**A view the mod borrows has to be given back, in two halves.** `FixedController` reads no input and
`Shift+C` has no `Fixed` case, so a player cannot take a borrowed view back by hand — anything that
borrows it owes a way out. The camera-mode menu changes the **mode** and leaves the follow; `[` and
`]` change the **follow** and leave the mode. `Sim/ViewClaim.cs` asks both, and a hand-back gives up
only the half the player did not take. The field of view is always handed back.
`docs/KSA-CAMERAS.md` has the evidence.

**The camera's roll is the engine's, and getting it needs the one extension point KSA leaves open.**
`FixedController` derives up from the followed object's frame, which is Identity for anything that is
not a `Vehicle` or a `Celestial`. `Ksa/LevelHorizonController.cs` subclasses it to supply the up
vector; installing it writes a get-only backing field by reflection, checks the write took, and
leaves KSA's own controller in place if the field ever moves.

## CI and releases

Building needs KSA's own assemblies. They are RocketWerkz's copyrighted files and **must never be
committed here or published anywhere**. They live in the private repository
**`LaurensDeV/ksa-game-assemblies`**, checked out by CI with a **read-only deploy key** held in the
`KSA_ASSEMBLIES_KEY` secret. Without the secret — a fork — the build job skips with a notice.

`Directory.Build.props` resolves the folder in tiers, first match wins: `KSA_DLL_DIR` (what CI
sets), then `Import/`, then a sibling `ksa-game-assemblies` checkout, then the game install.

- **`tooling` (hosted, always runs)** — `tools/check-all.sh`: shellcheck, the Sim/Ksa boundary, the
  XML, the asset references, the comment and doc rules, the meshes, and no tracked artefacts.
- **`build` (hosted)** — the build, the tests, `validate-parts.py`, the API surface and the package,
  against the checked-out assemblies.

**Work happens on `dev`; `main` is the release branch.** Merge `dev` into `main` to release, and
**merge, do not squash** — semantic-release reads the individual commits to build the changelog.
It runs on every push to `main`: version, `CHANGELOG.md`, the `<Version>` in the csproj (via
`tools/set-version.sh`), the tag and a **draft** GitHub Release; the second job builds the archive,
attaches it and publishes, so no release is public without its archive. **Never edit a version by
hand.** The changelog is written for players, so only `feat`, `fix`, `perf` and `build` appear in it.

`spacedock.yml` publishes what is attached to SpaceDock, and skips with a notice unless
`SPACEDOCK_MOD_ID` and `SPACEDOCK_USERNAME` (repository variables) and `SPACEDOCK_PASSWORD` (a secret)
are all set. It claims compatibility with the build in `ksa-assemblies.lock`. SpaceDock refuses a
changelog over 10,000 characters, and the archive with it, so `tools/spacedock-changelog.py` cuts one
that does not fit.

Three things that will bite: **branch protection on `main`** blocks the release commit unless the
token can bypass it; **a shallow checkout** makes every push look like a first release, hence
`fetch-depth: 0`; and **semantic-release carries on from the newest tag it can see**.

**One archive covers Windows and Linux**: the mod is a portable `net10.0` assembly. Case sensitivity
differs, which is why `validate-parts.py --offline` runs on Linux in CI against the real directory
listing. Release builds carry **no debug symbols**, and the log starts at `INFO`; the panel's
**Verbose log** puts `DEBUG` back without a different build. `package.sh` refuses to ship a `.pdb` or
any DLL that is not ours.

### After a KSA update

**You will be told when this happens.** `./tools/check-ksa-version.sh` compares RocketWerkz's
published build to the lock, and `ksa-version.yml` does the same daily and opens an issue.
`./tools/build.sh` checks the install against the lock on every build.

The assemblies exist in two places that drift apart silently: your `Import/`, and the private repo CI
compiles against. `ksa-assemblies.lock` records the expected SHA-256 of each referenced assembly plus
the game build, and both CI and `sync-import.sh` check against it. **Run the `upgrade-ksa` skill**,
which is this written out with the reasoning attached:

```bash
./tools/sync-import.sh                                    # refresh Import/; it reports the drift
./tools/sync-assemblies.sh      ../ksa-game-assemblies    # the mirror's DLLs
./tools/decompile-assemblies.sh ../ksa-game-assemblies    # the mirror's sources
#   set current/KSA_BUILD, commit BOTH together, push there
./tools/ksa-api-diff.sh ../ksa-game-assemblies      # what actually broke — read this
./tools/check-assemblies.sh --update                # record the new digests
./tools/api-surface.sh                              # the surface moves if the fixes did
#   edit the `build` line in ksa-assemblies.lock, commit it here
```

Then **recheck `docs/BLOCKED-ON-KSA.md`**: a KSA update is the only thing that changes any of it.

**The compiler only finds half of it.** A member that keeps its name and signature and changes its
*meaning* compiles clean and is wrong in flight; the decompiled corpus and `ksa-api-diff.sh` are for
that half.

The build number is written in seven places: `ksa-assemblies.lock`, `current/KSA_BUILD` in the
private repo, and the **KSA build** line under Environment above, `README.md`,
`docs/KSA-MODDING-NOTES.md`, `docs/KSA-CAMERAS.md` and `docs/BLOCKED-ON-KSA.md`. The lock is the
source of truth and `check-docs.sh` fails on any prose file that disagrees with it.

## Testing

`tests/KSAGolf.Tests` runs everything under `Sim/` headlessly. The suites worth understanding before
weakening: `DrawAnchorTests` (the two instants), `SkippedFrameTests` (a missed frame is integrated,
not lost), `FrameLatchTests` (the latch is always released), and `ContactSweepTests` (a step is not
stepped over).

**A behaviour change is unverified until it has been seen in game**, whatever the suite says. Since
the cut, only the mod loading with `KSAGolfProps.xml`, the bridge's `frame`, `capture` and
`kitten_prop`, and the putter and ball drawn on a kitten have been seen in game.

**Another developer install can answer the bridge.** A mod built from the same tooling with its own
`developer` file reads the same `Logs/bridge` folder, and whichever mod reads a command first answers
it. Replies missing fields, a `capture` that never answers, or a `frame` dropped at once mean another
mod took the command; its own log names it.
