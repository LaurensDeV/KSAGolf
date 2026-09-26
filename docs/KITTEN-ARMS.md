# A kitten holding something

**A record of what the engine lets a mod do with a kitten's arms and hands**, read against KSA
**2026.9.22.5482**; paths into the engine are relative to `../ksa-game-assemblies/current/src`. A club
is the thing to hold.

**What is in this tree:** the two probes that proved the engine half — `Ksa/KittenPoseProbe.cs`,
driven by the bridge's `kitten_pose`, and `Ksa/KittenPropProbe.cs`, driven by `kitten_prop` — and
`Ksa/AttachmentMesh.cs`, which builds a mod mesh to hang on a kitten.

**What is not built yet:** a grip that follows the hands, a two-handed reach, and a held item
described as data. The facts below are what they will be built on.

## The pose: `IAnimProcessor`

`AnimatedRenderable.AnimProcessors` is a public list, and `IAnimProcessor` a public interface.
`AnimatedRenderable.UpdateAnimation` samples KSA's own clip, hands the local pose to every processor
as a writable `Span<TransformTRS>` (`UpdateLocalPose`), and skins whatever they leave there. Core's
own kitten face, eyes and ears are processors in the same list (`KittenRenderable.cs:211-237`), and
`CatEyeAnim` is a look-at — IK in miniature.

It runs **inside** the render, from `KittenRenderable.UpdateRenderData`, after KSA has animated and
before the attachments read `GetBoneTransform` (`:355` against `:399`). So unlike an attachment's
mesh transform there is nothing later to overwrite it, and anything socketed to a hand follows the
hand the processor moved, in the same frame.

Reaching the list takes one reflection hop: `KittenEva.Renderable` is public,
`KittenRenderable._characterAvatar` is not.

The rig is a standard biped arm: `Shoulder_R/L`, `Elbow`, `Wrist`, two twist bones on each segment
and five fingers, with `Spine1_M` and `Spine2_M` above. `Skeleton` exposes names, parents and local
and world transforms, which is what two-bone IK needs. `CatPostAnim.MixPoseLocal` blends a subtree
towards a pose by a weight, which is "arms from a holding pose, legs from the walk".

Past about 45 m times a processor's `Priority` the engine stops calling it
(`KittenRenderable.UpdateAnimationLod`); the model itself stops animating at the same distance. A
kitten nobody is looking at is never rendered, so its processors are never called at all.

**One skeleton, rebuilt per view.** `AnimatedRenderable.UpdateAnimation` runs its local pose once a
frame but rebuilds the world transforms and calls every processor's `UpdateSkeleton` for each view
that draws the kitten, and skins each view separately. So a processor can give the main view one
pose and every other view — and the shadow — another, which is how a first-person viewmodel is
built. A prefix on `KittenEva.UpdateRenderData` is where to learn which view is being drawn.

### `kitten_pose`

One turn inline — `bone` (default `Elbow_R`), axis `x`/`y`/`z` (default `0, 0, 1`), `deg` (default
45) — or `turns: [...]`, each with `bone`, `x`/`y`/`z`, `deg` and optionally:

| | |
| --- | --- |
| `swing`, `hz`, `phase` | vary the turn by `swing` degrees at `hz`, phase in degrees |
| `shape` | `Swing` (either side), `Bounce` (one side, a hop) or `Spin` (round and round) |
| `from`, `to` | seconds into the routine the turn is active, eased in and out |
| `move` | shift along the axis in the parent's frame, `deg` read as model units |
| `stretch` | scale the bone along the axis by one plus the amount, carrying its children |

Top-level: `loop` (seconds), `rate`, `rest` (pose from rest rather than on top of KSA's clip), `fly`
(take control of a kitten so it renders), `mmu` and `helmet` (show or hide those attachments — the
MMU's hand controllers ride the forearms and hide the hands), `orbit_az`/`orbit_el`/`orbit_zoom`
(turn the orbit camera), `report: true` (list the bones), `on: false` (let go).

## The held thing: `CharacterAvatar.CosmeticAttachment.Transform`

The renderer writes `Mesh.Transform = attachment.Transform * (body * bone)` every frame and draws
straight after (`KittenRenderable.cs:403`), so the mesh's own transform cannot be aimed.
`attachment.Transform` is a public field set once at construction and composed rather than
overwritten, so writing *it* can. `CosmeticAttachments` is a public list, so a mod can add a prop to
a kitten at runtime without declaring a `<Character>` — which matters, because a mod character is
dressed onto a quarter of every roster and crashes a launch through Borea
(`docs/KSA-MODDING-NOTES.md`).

**Flown: both halves work.** `kitten_prop` builds a `StaticMeshRenderable` from
`Program.Instance.SuperMeshRenderSystem`'s public renderers and adds it to every EVA kitten's
`CosmeticAttachments`. The one reflection is the asset id, an `AssetName` from `Planet.Core.dll`,
which this mod does not reference. The default stand-in is a copy of Core's own helmet, already
loaded with its materials, so nothing but the attachment path is under test. At full scale the
helmet sat in the kitten's posed hand with her own head bare behind it, and rewriting the
attachment's `Transform` with 60 cm on y moved it to the ground beside her, paused, with nothing
else changed. The engine draws the mesh only for a kitten on camera, like the pose.

### `kitten_prop`

`mesh` (default `helmet`), `bone` (default `Wrist_R`), `scale` (default 0.3), turns `rx`/`ry`/`rz`
in degrees and offset `x`/`y`/`z` in the attachment's own centimetres — applied scale, then the
turns about x, y and z, then the offset, before the bone — or a whole `matrix`; `on: false` takes it
off.

Any `mesh` other than `helmet` is an asset prefix: `P` hangs the declared pair `P_Glb` and
`P_Material`. A mod's own mesh goes through `AttachmentMesh`, by the attachment rules: centimetres baked into the
vertices, one mesh, one primitive, and the export must name a material slot — `AttachmentMesh`
fills it before the renderable is made, and with no slot building it throws.

## What the rig is like to hold things with

Measured on the shipped kitten; these are facts about the rig.

- **Fingers.** Every finger's **local y is the curl** (+y into the palm); the thumb's second and
  third joints curl on +z. The idle hand is a tight fist: first joints 73–91° from straight, second
  joints 143–154°. Straightening each chain against those readings opened the paw flat to within 2°.
- **Reach.** The arm is 7.4 cm shoulder to elbow and 7.3 elbow to wrist, and the shoulders are 26 cm
  apart. Nothing long held in both hands at its real size reaches both grips; a two-handed hold has
  to scale the object or turn the upper body.
- **The idle sways.** A pose laid over KSA's live idle breathes with it — flown unaimed, a held
  barrel wandered from level to 8° down. Capturing the idle's rotation of every bone between the
  spine and the hand when the hold begins, and holding it, leaves the root and so walking to KSA and
  steadies the rest. Which moment is captured differs by a couple of degrees from one hold to the
  next, so anything that must be level is levelled after.
- **Level is the world's, not the spine's.** The kitten leans; laid square to its spine a held
  object pointed 6° down.
- **Skeleton space against drawn space** is related by a least-squares fit over bones both reports
  carry, to 34 nm.

## Engine facts it cost to learn

- `KittenEva` draws the model at its origin **less the centre of mass**, so a point measured off the
  model's drawn origin is half a kitten out.
- KSA takes a walking kitten's forward from the camera's, flattened onto the ground, and within about
  3° of straight up or down falls back on the camera's up, which points behind the view. A camera
  looking steeply down sends a kitten walking backwards or sideways. A prefix on
  `KittenLocomotion.StepGrounded` can hand it a heading instead.
- The main view's draw puts whatever it skins into the shadow pool. A postfix on
  `AnimatedRenderable.Draw` can take those entries out and skin the body again into the same
  per-frame bone buffer for the shadow, so the shadow is the whole kitten while the view draws only
  arms.
- `KittenEva.LocomotionState.Mode` says whether the kitten is on the ground.

## Open

Turning the whole kitten to face something is unexplored: it is a `Vehicle`, but whether KSA's EVA
locomotion fights a commanded heading is not known. The bridge cannot walk a kitten, so nothing that
depends on moving has been flown from it.
