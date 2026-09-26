# Coming from KSP modding

If you have written parts or plugins for Kerbal Space Program, most of your instincts transfer and
three of them will actively mislead you. This page is the translation, and the three that mislead
are named first.

It maps *concepts* — `PartModule`, `ConfigNode`, ModuleManager, `GameData` — that anyone who has
modded that game already knows. No mod's source was consulted or copied; these are the parts of
KSP's own API that every part author has used.

## The short version

| In KSP | Here |
| --- | --- |
| `GameData/<Mod>/` scanned at startup | `mods/KSAGolf/`, listed in the user directory's `manifest.toml` |
| `.cfg` in `ConfigNode` format | XML asset files at the mod root, each listed in `mod.toml` |
| `PART { }` | `<Part>` and `<SubPart>` |
| `MODULE { name = X }` binding a `PartModule` | **nothing. See below.** |
| ModuleManager patching (`@`, `%`, `:NEEDS`) | nothing; there is no patch layer |
| `model.mu` | `.glb`, in a mesh atlas |
| `PartResource` / resource flow | nothing a mod can add to; count it yourself |
| KSP's own `Debug.Log` | `Ksa/Log.cs`, writing `Logs/KSAGolf.log` |

## The three that will mislead you

### 1. There is no `PartModule`

This is the big one, and everything else in the architecture follows from it.

KSA has **no way for a mod to register a behaviour that the engine then calls on a part.** Its
per-frame update lists are internal and are not reachable without patching the game. So the KSP
reflex — write a class, name it in a config, let the game drive it — has no equivalent.

What a mod does instead: the part is **inert structure with mass and a collider**. The mod finds it
on a vehicle by part Id and runs the behaviour from its own frame hook. A part is a *thing to find*,
not a thing that acts.

The consequence you will feel: **behaviour is not per-part-instance by default.** If you want two
copies of a part on one craft to behave differently, that is something you build — a roster keyed on
the craft and the part, with settings of its own — not something the engine gives you.

`docs/BLOCKED-ON-KSA.md` lists this and everything else the mod wants and cannot have, with the
engine reason for each. If RocketWerkz ever ship a module system, that file is where the change
starts.

### 2. A fast body need not be a physics object

In KSP anything that flies is a vessel: it has parts, colliders, and the physics engine moves it.

Here a body can be a **number integrated by the mod**, drawn as a subpart or an attachment. That
suits anything fast, because the engine steps a vehicle once a frame and a fast body crosses metres
of anything thin in one frame. Collision is then something the
body asks for — `Sim/ContactSweep.cs` sweeps a step, `Sim/IGroundTest.cs` and `Sim/IHullTest.cs` ask
where the ground and a craft's hull are — rather than something it gets.

So: do not look for a rigidbody, and do not expect `OnCollisionEnter`. What the mod simulates itself
is pure maths under `Sim/`, tested headlessly with no game running at all.

### 3. Everything is in a heliocentric frame, and it is moving at 29.8 km/s

KSP gives you one origin that is, for practical purposes, still. KSA does not.

Positions are in the ecliptic frame. Earth is travelling at about **29.8 km/s** through it, so a
one-frame error in *which instant* two positions were sampled at is not a rounding error — it is
**~500 m at 60 fps**. It surfaces as a jitter, a constant offset, a steering error or a drift, which
are four disguises for one mistake.

**Read `docs/FRAMES-AND-EPOCHS.md` before touching anything the mod moves, draws or times.** It is not
general advice; it is the engine's actual contract, the rules that follow from it, and how to tell
the four failure shapes apart.

## What is the same

- **Parts are declared as data and reference assets by Id.** The XML is stricter than
  `ConfigNode`, but the shape is familiar: declare a mesh, declare a material, declare a part that
  uses them.
- **Art is separate from behaviour.** Meshes are authored in Blender (`tools/model/README.md`);
  nothing in the simulation names them.

## Adding a part

1. **Model it** in Blender, to the export contract in `.claude/skills/ksa-blender/SKILL.md`.
   `tools/model/checkmesh.py` fails on the two defects that are only visible in game.
2. **Declare it.** A `<SubPart>` per moving assembly plus a `<Part>` in an asset XML at the mod root,
   a `<PartGameData>` with colliders and mass, and the file listed in `mod.toml` — a file left out of
   that list is never loaded, and nothing reports it.
3. **Validate it.** `tools/validate-parts.py` checks asset Ids, mesh Ids against the atlas and
   texture paths against disk. A bad Id or path is a *silent* in-game failure.

**A shipped part's subpart list is append-only, and its Id is not renameable**: a save pairs a part
with its definition positionally and names it by Id, and breaking either terminates the game on every
save holding it. `docs/KSA-MODDING-NOTES.md` has the loop.

## Where the rules are written down

| Read this | Before |
| --- | --- |
| `docs/FRAMES-AND-EPOCHS.md` | touching anything the mod moves, draws or times |
| `docs/KSA-MODDING-NOTES.md` | anything that calls into KSA |
| `docs/BLOCKED-ON-KSA.md` | proposing a feature that needs an engine hook |
| `CLAUDE.md` | committing — the message format is enforced and decides releases |
| `CONTRIBUTING.md` | setting up: `./tools/doctor.sh` tells you what is missing |

Two rules that are not negotiable:

- **A behaviour fix is unverified until it has been flown.** Compiling, passing the suite and
  having a plausible mechanism are not evidence. The hardest bugs here live in the gap between the
  maths and what KSA actually does, and that gap is only visible in the game.
- **A regression test only counts if it fails against the old code.** Check that it does, every
  time. A test that advances the anchor by exactly the `v*dt` it passes in cancels its own error
  and passes against the broken implementation too, which looks like proof and is worth nothing.

## What you do not need

You do not need **the game running, Blender, or a Windows box** to work on the simulation.
Everything under `src/KSAGolf/Sim/` is free of KSA types by construction: the test project links it
wholesale and references no KSA assembly, so a `using KSA;` there fails the test build.

What you *do* still need is **KSA's assemblies**, even for that half: `double3` comes from
`Brutal.Core.Numerics.dll`, so the test project references it and `tools/test.sh` will not run
without it. `./tools/doctor.sh` says what is missing and how to get it.
