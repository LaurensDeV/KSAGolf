# Model tools

**All art is authored in Blender over MCP** — `.claude/skills/ksa-blender/SKILL.md` is the procedure
and the export contract. There is no headless generator: an authored asset's `.blend` is its source
and is not in this repository, so what is committed is the export, and the checkers here are the only
gate on something nobody can rebuild from a clean checkout.

| Tool | What |
| --- | --- |
| `checkmesh.py` | zero-UV-area triangles, coplanar faces and unpaired node/mesh names in a `.glb`; `--compare` diffs two exports by geometry *and* node transform; `--units-per-metre 100` for a character attachment |
| `preview.sh` | renders an exported `.glb` from a few angles, from WSL, so it can be judged before it is declared |
| `preview-glb.py` | what `preview.sh` runs inside Blender |
| `dilate-atlas.py` | fills the empty space round a baked atlas's islands from their nearest neighbour |
| `smoketest.py` | proves headless Blender can build, render and export |
| `../meshinfo.py` | a mesh's bounds out of a `.glb`, so "is it 3 m or 4 m wide" is a fact |
| `../validate-parts.py` | asset Ids, mesh Ids against the atlas, texture paths against disk, subpart contact |

```bash
./tools/model/checkmesh.py src/KSAGolf/Meshes/<atlas>.glb
./tools/model/preview.sh src/KSAGolf/Meshes/<atlas>.glb
./tools/meshinfo.py src/KSAGolf/Meshes/<atlas>.glb <mesh>
```

## Coordinate system

Part space, which is also glTF file space: export with `export_yup=False`, so Blender coordinates
*are* what KSA reads out of the atlas and what `<Transform>` and `<LocationAsmb>` in the part XML
mean. **+X is a part's forward axis in KSA's sense** — up, for a part standing on a stack node. The
skill has the rest of the contract: origin on the mounting face, node names matching mesh names, a
`_VM` twin per body, `_ColPrim_` boxes for colliders.

## Materials

The PBR texture is **R = ambient occlusion, G = roughness, B = metalness**. KSA's own
`Content/Core/Textures/default_pbr.png` is `(255, 180, 0)` and `EmptyAoRoughMetallic.png` is
`(255, 255, 0)` — unoccluded, rough, non-metal — matching the `<AoRoughMetal>` element name and the
glTF ORM convention.

**KSA loads PNG for material slots**, so no `.ktx2` encoder is needed. `CharacterAssets.xml` mixes
`.ktx2` and `.png` in the same `<PbrMaterial>`.

## Every face needs UV *area*

A face whose loops share one UV — the obvious way to paint a flat swatch — has a zero UV derivative.
A renderer building a tangent frame from that gets a zero-length tangent; `normalize()` on it is NaN,
and NaN survives being multiplied by zero, so even a *flat* normal map cannot rescue the shading. In
game the part crawls with flickering white speckle, changing as the camera moves. Give every face a
small projected patch instead; orientation and scale are cosmetically irrelevant so long as they are
non-degenerate.

**Blender's preview render does not reproduce this** — the preview material has no normal map wired
in, so the previews look perfect while the part sparkles in KSA. `checkmesh.py` finds it.

## Never let two faces share a plane

Two coplanar faces make the depth buffer pick a winner per pixel per frame, and in game the part
crawls with speckle that the preview does not show. A uniform inflation only separates faces pointing
*at* each other; two faces pointing the same way on the same plane need *different* offsets. Two
coaxial cylinders a couple of millimetres apart fight even though nothing is coplanar — use a
different facet count, or a cone.

**Both defects look identical in game**, so diagnose with the checker, not by eye: inflating geometry
that is already fine fixes neither. `checkmesh.py` reports coplanar overlaps with their true
intersection area, plus parallel faces within a few millimetres.

**Two bodies can share a plane, and `checkmesh.py` alone will not see it** — it analyses one mesh at
a time, and the atlas carries no node transforms. The cross-body pass is in `validate-parts.py`, which
reads each subpart's `<Position>` and `<Rotation>` from the part XML.

## An export is not byte-reproducible

Blender's glTF exporter does not emit triangles in a stable order, so exporting unchanged geometry
produces identical positions, normals and UVs and a **permuted index buffer**. `git status` showing an
atlas as modified means nothing on its own. Ask

```bash
./tools/model/checkmesh.py <new.glb> --compare <old.glb>
```

which compares the surface itself — every triangle canonicalised and sorted — and **revert the atlas**
if it reports the same geometry.

## Traps in a hand-built mesh

A mesh built with `bpy.data.objects.new` and `from_pydata` rather than a primitive operator:

- **Is not selected**, and `transform_apply` acts on the *selection*, not the active object — so its
  transform is never baked and it comes out correctly shaped and in the wrong place.
- **Has whatever winding you listed.** An inward face is culled in game, so the part reads as hollow
  from one side. Run `bmesh.ops.recalc_face_normals`. Blender does not backface-cull by default, so a
  preview will not show it.

And a primitive operator **already makes a UV layer**: calling `uv_layers.new()` adds a *second* one
and leaves the generated one first, which is the one the glTF exporter writes as `TEXCOORD_0`.

## Blender from WSL

- **Blender is a Windows binary.** A WSL path passed to `--python` gets mangled: `wslpath -w` the
  script, and give Blender Windows paths (`C:\...`) for output. `preview.sh` does both.
- **Blender 5.2 has no `BLENDER_EEVEE_NEXT`**; use `BLENDER_EEVEE`.
- **`TRACK_TO` aligns its up axis to world Z**, and a part's forward is X, so a tracked camera comes
  out rolled ninety degrees. Build the camera matrix directly.

`smoketest.py` builds two primitives, renders a PNG and exports a GLB. Run it first after any Blender
or WSL change — it proves the toolchain before you debug a model against a broken one.

```bash
BL="/mnt/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"
"$BL" --background --python "$(wslpath -w tools/model/smoketest.py)" -- 'C:\Windows\Temp\out.png'
```

`docs/KSA-MODDING-NOTES.md` has the part XML that consumes all of this.
