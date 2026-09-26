# Where KSA thinks the ground is

Everything below comes from the decompiled corpus at `../ksa-game-assemblies` against build
**2026.8.22.5348**, from the shipped `Content/Core/Astronomicals.xml`, and from measurements taken
directly off the shipped `Earth_Height.ktx2`. Nothing here has been flown. The measurements were
taken against 2026.8.19.5261 and still stand: they are sampled well inside a face, and the only
sampling change since is in the seam branch.

**Why it matters.** Anything that stops on, rolls over or is placed on the ground asks this field,
and several callers asking it have to agree about where the surface is. A metre of disagreement
costs `cot γ` metres of ground for something arriving at angle `γ` — 9.3 m at six degrees, 11.1 m at
five, 2.7 m at twenty — so a surface question that is a rounding for a steep arrival is a
first-order term for a shallow one.

The mod samples the surface in three places:

| | call | surface |
| --- | --- | --- |
| `Ksa/GroundTest.cs` | `GetTerrainHeightFromDirCce(dir, accurate: true)`, clamped to the waterline | where a mod-simulated body stops |
| `Ksa/KsaWorld.cs` | `GetTerrainHeightFromDirCce(dir, accurate: true)` | the cursor's ground point, and a point placed on the ground |
| `Ksa/TerrainHeights.cs` | `GetTerrainHeightFromDirCce(dir, accurate)` | a sightline walked over the skyline, `accurate: false` unless asked |

A caller holding a body-fixed direction — one from `GetDirCcfFromLatLon`, say — must use the `Ccf`
entry point instead; see [Cce against Ccf](#cce-against-ccf). Clamping to the waterline matters more
than it sounds; see [The sea](#the-sea).

## The field itself

`Celestial.GetTerrainHeightFromDirCcf` (`Celestial.cs:792`) is the only height query in the engine.
Every other entry point funnels into it: `…FromDirCce` (`:780`) and `…FromDirCci` (`:786`) rotate the
direction into `Ccf` and call it; `GetSurfacePositionEclFromDirCce` (`:770`) calls it and multiplies
out to a position.

It is a **cubemap lookup plus a procedural stack**:

```csharp
if (!HasTerrainHeightmap) return 0.0;
double num2 = MathEx.Lerp(a: HeightReference.Minimum, b: HeightReference.Maximum,
                          t: accurate ? SampleHeightBicubic(...) : SampleHeightMapBilinear(...));
if (NormalReference is null || !NormalReference.TextureAsset?.Texture.IsCubemap) return num2;
...
if (BiomeMaterials?.NumBiomeMaterials is not > 0) return num2;
... for each terrain modifier ...
    if (accurate) modifierReference.Evaluate(..., ref heightKm, ref gradient);
return heightKm;
```

For Earth (`Astronomicals.xml:522`):

| | |
| --- | --- |
| height map | `Earth_Height.ktx2`, **4096 × 4096 per face**, 6 faces, `R16_UNORM`, 13 mips, uncompressed (268 MB) |
| range | `Minimum Km="-10.930"`, `Maximum Km="8.631"` — `DistanceReference` multiplies `Km` by 1000, so **metres** |
| modifiers | eleven: **Erosion** (amplitude **1000 m**, 7 octaves), four **TilingDetail** (**1900**, **1500**, **1400**, **225 m**), **Dunes** (**1500 m**) and five zero-amplitude **Decal**, one per launch site |
| biomes | seven, with a `<BiomeMaterials>` block — so the modifier loop **does** run on Earth |
| ocean | `<Level Km="0"/>` |

**A launch site is a levelled disc, not terrain.** A zero-amplitude `Decal` with `Additive="false"`
reduces to `heightKm = lerp(heightKm, AltitudeOffset, mask)`, so within its `Radius` — 275 m at
every shipped site — the field answers a fixed altitude smoothed to the rim rather than whatever
the cubemap and the noise say. Something placed on a pad sits on flat ground, and the
static object standing on it is a separate question; see `docs/BLOCKED-ON-KSA.md`.

### Resolution

A cube face is a gnomonic projection of a 90° quadrant, so texel spacing on the ground is not
uniform. On Earth (`MeanRadius` 6,371 km, 4096 texels across a face):

| | ground spacing |
| --- | --- |
| face centre | **3,111 m** |
| face edge midpoint | 1,556 m and 2,200 m on the two axes |
| face corner | **1,466 m** |

So the *base* field is a 1.5–3.1 km grid. Everything finer than that on Earth comes from the
procedural modifiers — which are analytic, but **not** unlimited in resolution: they are evaluated
on a direction packed to `float3`, which is a floor of about a third of a metre of ground. See
[The modifiers run in single precision](#the-modifiers-run-in-single-precision) for that and
[Where the detail actually comes from](#where-the-detail-actually-comes-from) for what fills the
scales in between.

### Quantisation

`R16_UNORM` over a 19,561 m declared range is **0.2985 m per level**. That is the floor under any
answer this field can give, and at a six-degree arrival it is **3.40 m of ground**. The procedural
modifiers are continuous and add on top, so the *returned* height is not quantised; the base term is.

### The modifiers run in single precision

**The base bicubic is `double` end to end; the procedural stack on top of it is not.** On a body with
a normal cubemap *and* biome materials — Earth has both — `GetTerrainHeightFromDirCcf` packs the
direction down before the modifier loop:

```csharp
float3 float6 = float3.Pack(in vector);        // Celestial.cs:833
float heightKm = (float)num2;                  // Celestial.cs:842
modData = new ... { Position = float6, TextureNormal = float6, ... };
```

`float3.Pack(in double3)` defaults to a plain cast, so the direction every modifier is evaluated at
is a single-precision unit vector. Its neighbours are `2^-24` to `2^-23` apart, which on Earth is
**0.38 m to 0.76 m of ground**; measured by walking a great circle and counting distinct packed
directions, the tread is **0.31 m** and the worst displacement over 20,000 random directions is
**0.307 m**.

So below about a third of a metre the modifier stack answers with one value: **the surface is a
staircase.** It is deterministic and identical for every caller, so it biases nothing: every caller
reads the same treads. What it does is put a floor under how finely the surface can be asked about
at all — and two callers resolving one crossing at different places land on *different treads*,
which scatters them even though neither is biased. For a ball rolling on a green, a third of a metre
is the size of the step it rolls over. The base term is unaffected: `SampleHeightBicubic` is
`double` throughout and its texel samples are exact 16-bit integers.

A body with no normal map returns before any of this, in `double`.

### Where the detail actually comes from

Between the base grid and that staircase there are two mechanisms and one gap, all on Earth:

| scale | what shapes it |
| --- | --- |
| coarser than **3,111 m** | the base cubemap, Catmull-Rom |
| 3,111 m down to **166 m** | `EarthErosion` — 7 octaves, lacunarity 2, gain 0.5, sampled at `direction x 600 x 2^i`, so 10.6 km down to 166 m of wavelength at 500 m down to 7.8 m of amplitude |
| 166 m down to **7-19 m** | the four `TilingDetail` modifiers — each a 4096-square `R16` texture whose UV is `direction x Frequency`, giving 7.4 m/texel (Alpine, f=209) to 20.5 m/texel (Desert Mountains, f=76) |
| **10 m down to 0.38 m** | nothing: a bilinear ramp between two detail texels, which is locally a tilted plane |
| below **0.38 m** | the float staircase above |

Each erosion octave carries an undamped slope of up to **0.30** — amplitude halves as frequency
doubles, so every octave contributes the same. What survives is that times the biome weight, times a
gradient-falloff power of the angle between the texture and surface normals, times `1 - |dot|` of
those two again, all near zero over flat ground. **The product is unmeasured here**; only the
geometry is.

### Interpolation

- `accurate: false` → `SampleHeightMapBilinear(TextureReference, double3)` (`Celestial.cs:1077`).
  Four texels, `-0.5` texel offset, and a proper cross-face wrap through `GetAdjacentFaceUv` for taps
  that fall off the edge.
- `accurate: true` → `SampleHeightBicubic(TextureAsset, direction, lod: 0)` (`Celestial.cs:1585`).
  Catmull-Rom over a 4×4 texel neighbourhood, each tap a point fetch through `SampleCubeFacePointR`,
  seams handled by re-deriving the direction and re-projecting. `lod` is a parameter and is always
  passed 0 — there is no exposed way to ask for a coarser one, and `SampleCubemapFaceSingleChannel`
  reads mip 0 regardless, so a non-zero `lod` would be a latent bug rather than a feature.

  **The seam branch changed in 2026.8.22.5348**, and only that branch — a tap landing off its face
  is now unfolded through `UnfoldCubeFaceUv` and point-fetched, where it used to be a bilinear
  blend of four `FetchTexelSeamlessR` taps taken at a `-0.5` texel offset. So a height within a
  face is the same number it was, and one within two texels of a cube edge is not.

The two face-selection routines are written twice — `DirectionToCubemap` (`:1454`) for the bicubic
path and an inlined copy inside the bilinear one — and they **agree**, face for face and axis for
axis. Checked term by term.

**Measured disagreement between them** on the shipped Earth cubemap, 199,154 directions drawn
uniformly on the sphere, sampled well inside a face so the seam paths never differ:

| | height | ground at six degrees |
| --- | --- | --- |
| mean | 4.21 m | 39 m |
| median | 1.21 m | 11 m |
| 99th percentile | 44.0 m | 410 m |
| max | **186 m** | **1.73 km** |

Above sea level only, the numbers are much the same (mean 4.37 m, max 167 m). **And that is the
interpolation alone.** `accurate: false` *also* skips every `Evaluate` call in the modifier loop, so
on Earth it drops erosion, tiling detail, dunes and the launch-site decals entirely — the last of
which means a coarse sample at a pad answers with the *unlevelled* terrain. Their declared
amplitudes total several kilometres; what any one direction actually loses is that scaled by biome
weight, by the noise value and by the slope factors inside `Evaluate`, so the real figure is
unmeasured here and somewhere between hundreds of metres and kilometres of height where those
biomes apply. Either way it dwarfs the interpolation term.

### `accurate: false` is not as cheap as it looks

The early returns are before `SampleNormalMap`, not before the loop. On a body that has a normal
cubemap **and** biome materials — Earth does — `accurate: false` still pays for the normal-map
bilinear fetch, the tangent frame, the biome ID and control samples, and one LUT evaluation per
modifier. All it saves is the bicubic (16 taps against 4) and the modifier `Evaluate` bodies, which
for Earth's erosion is seven octaves of gradient noise. That is the expensive part, so the saving is
real — but a body with no normal map returns after the interpolation and the two paths then differ
only by 12 texel fetches.

`Ksa/TerrainHeights.cs` is where the mod asks `accurate: false`, for a sightline sampled many times
per look. KSA's own `TerrainImpactFinder` makes the same choice for the same reason
(`TerrainImpactFinder.cs:64`).

## Is `accurate: true` the surface the game itself uses?

**For physics, yes — the identical call.**

- `PhysicsEnvironment.RecomputePositionalValues` (`PhysicsEnvironment.cs:112`) sets
  `TerrainRadius = MeanRadius + GetTerrainHeightFromDirCcf(dir)`, default `accurate: true`.
- `TerrainPatch.GetTerrainRadiusCcf` (`TerrainPatch.cs:518`) does the same, and
  `GetTerrainVertexCcf` places every vertex of the collision patch on it. The patch grid step is
  **2 m** (`TerrainPatch.UpdateState` initialises with `2.0`), so the surface a craft rests on is a
  2 m triangulation of exactly the field the mod samples continuously. Against the finest erosion
  octave above, the chord error of that triangulation is of order **a centimetre**.

So a landed craft and a body that stops on `GroundTest`'s answer are standing on the same surface.
There is no separate collision height field.

**For rendering, the same field through a second implementation.** The terrain mesh is displaced in
`Content/Core/Shaders/Planet/TerrainMesh/PrepareModifiers.comp`, which reads the same cubemap with
`SampleHeightmapBicubicTrilinear(planetHeightMap, …, mipLevel)` — bicubic like the CPU, but at a
**mip level derived from the vertex spacing**, and then runs the same modifier set in
`ProceduralModifiersLibrary/ProceduralModifiers.comp`. Two consequences:

- Where the mesh is coarse — far from the camera — the drawn surface is a filtered, smoother version
  of what the CPU reports. Close up the mip goes to zero and the two converge.
- The modifiers are implemented twice, in C# and in GLSL. They are the same functions with the same
  parameters, but they are not the same code and nothing enforces that they stay in step.

The practical rule: **the CPU `accurate: true` answer is authoritative for where things happen, and
the GPU is a separate rendering of it.** A body that stops where `GroundTest` says will not be
visibly floating, because the physics agrees; whether it is a pixel above or below the drawn mesh at
a given camera distance is a rendering question.

## Cce against Ccf

**Not a bug. The mixed use is correct, and the two entry points are not interchangeable.**

| frame | what it is |
| --- | --- |
| `Ccf` | body-**f**ixed. Rotates with the planet. `+Z` is the spin axis. |
| `Cci` | body-centred **i**nertial. Same origin, does not rotate. |
| `Cce` | body-centred, **e**cliptic axes. Same origin, ecliptic orientation, so a *direction* in `Cce` is the same direction in `Ecl`. |

`GetTerrainHeightFromDirCce` (`Celestial.cs:780`) is one line plus a delegation:

```csharp
double3 positionDirCcf = positionDirCce.Transform(GetCcf2Cce().Inverse());
return GetTerrainHeightFromDirCcf(positionDirCcf, accurate);
```

So the `Cce` entry point applies the body's rotation *for you*. That is exactly what `GroundTest`
needs, because it starts from `unit(positionEcl − centreEcl)` — an ecliptic direction with no
rotation taken out. A direction that is already body-fixed (from `GetCci2Ccf()` or
`GetDirCcfFromLatLon`) belongs in the `Ccf` entry point. Passing either kind to the other entry point
rotates it by the planet's current phase — 465 m/s × (time since epoch) of error on Earth.

The rotations all come from one per-frame snapshot and cannot drift against each other:
`UpdatePerFrameData` (`:594`) sets `_ccf2Cci = GetCcf2Cci(Orbit.StateVectors.StateTime)` and
`_ccf2Cce = _ccf2Cci * _cci2Cce`, and `Universe` calls it once per frame after applying the step
(`Universe.cs:1683`). `GetCci2Ccf()` and `GetCcf2Cce()` are both derived from that same `_ccf2Cci`,
so the `Cce` path and the `Ccf` path are in phase by construction.

## The round trip is exact

A point placed at

```
dirCcf(lat, lon) · (MeanRadius + h(dirCcf)) · Ccf2Cce + bodyEcl
```

is recovered by `unit(pos − bodyEcl)` = the same `Ccf` direction after `Cce2Ccf`, which reads the
same `h`. `Celestial.GetDirCcfFromLatLon` (`:670`) and `GetLatitudeFromCcf`/`GetLongitudeFromCcf`
(`:708`, `:743`) are exact inverses — `(cos φ cos λ, cos φ sin λ, sin φ)` against `asin(z)` and
`atan2(y, x)` — and both sides use the same cached `_ccf2Cce`, so the trip closes to floating-point.
Keep latitude and longitude in `double`: 0.001° of latitude is **111 m** of ground.

## The sea

The height field answers with **terrain**, which under an ocean is the seabed. `GroundTest` clamps
it to the waterline through `GroundSurface.Height` (`Sim/IGroundTest.cs`):

```csharp
if (nearest.GetOceanReference() is { } sea && sea.Density > 0.0) { hasSea = true; seaLevel = sea.Level; }
height = GroundSurface.Height(height, seaLevel, hasSea);
```

Without the clamp a body falls through the waterline and stops on the bottom, and every caller that
must agree with it has to clamp the same way. Measured over all 100,663,296 texels of
`Earth_Height.ktx2`:

| | |
| --- | --- |
| below the waterline | **71.2%** of the surface |
| mean depth | 3,776 m |
| median depth | 4,180 m |
| deepest | 10,930 m |

Over dry land the terrain is above the waterline and the clamp is a no-op, which is why an inland
test never shows a missing one.

**One residual, and it is small.** KSA's own physics uses `OceanRenderer.GetOceanHeightAtPositionCcf`,
the *displaced wave* surface, not the flat level (`PhysicsEnvironment.cs:120`). `OceanReference.Level`
is the flat one, so a mod using it agrees with itself and not with the engine, by the height of the
waves.

## Is there anything more exact?

**No.** `accurate: true` is the most exact surface query a mod can reach. Everything else in the
engine either calls it or is coarser:

| | |
| --- | --- |
| `GetTerrainHeightFromDir{Ccf,Cce,Cci}(dir, accurate: true)` | the field itself, bicubic at mip 0 plus every modifier. **This is the ceiling.** |
| `GetSurfacePositionEclFromDirCce(dir, accurate)` | the same call, returned as a position |
| `Celestial.SampleHeightMapBilinear(TextureReference, double3)` | `public static`, raw base texture, no modifiers — strictly coarser |
| `Celestial.SampleCubemapFaceSingleChannel(asset, texel, face)` | `public static`, one raw texel |
| `TerrainImpactFinder.TryFind(body, trajectory, …)` | `public static`, does the crossing bisection for you — but samples `accurate: false` internally, so it is coarser than asking `accurate: true` yourself. 24 bisection iterations plus a coarse march. |
| `TerrainPatch` / the physics collision mesh | its public entry points want a `ReadOnlyPhysicsStates` ref struct a mod cannot construct, and its vertices come from `accurate: true` anyway |
| `KSA.Rendering.BoundingVolumeHierarchy` | a builder for the raytracing acceleration structure, not a query API; its terrain triangles come from the same call (`:651`) |

There is **no raycast**, no collider query, and no higher LOD. The cost of the exact call is roughly
16 texel fetches for the bicubic plus the normal, biome and LUT work plus the modifier evaluation —
for Earth, seven octaves of gradient noise and four texture-driven detail lookups. Asking only near
the surface, and answering with the mean sphere above the highest terrain, is what keeps it
affordable.

## Two smaller things worth knowing

**`MaxTerrainHeightApprox` is a sample, not a bound.** `Celestial.UpdateApproxTerrainAltitudes`
(`:931`) computes it once, in the `Celestial` constructor — before `Universe.SetupRenderData`
populates the modifiers, so erosion, dunes and detail contribute nothing — from **16,384**
Fibonacci-spiral directions, about one sample per **176 km** on Earth
(`sqrt(4*pi/16384) x 6,371 km`). Measured against the shipped `Earth_Height.ktx2` it returns
**~5,692 m** where the base field alone reaches **8,011 m** (and the template declares 8,631). So a
sphere of that radius does not contain the terrain, and a cheap reject standing in front of an exact
test on it produces false negatives. `Astronomical.MaxTerrainRadius` (`Astronomical.cs:116`) is
`MeanRadius + HeightReference.Maximum` — a hard bound on the base texture that does not bound the
modifiers standing on top of it; `KsaWorld` adds the modifiers' summed amplitudes to it.

**Sampling the ground once a frame holds it across every sub-step.** A body that asks for the ground
once per frame and stops on it across its sub-steps stops on the surface as it was up to one frame
of ground track behind: with slope `s` and arrival angle `γ`, the error is about
`s·Δ / (tan γ + s)` where `Δ` is the ground track covered in a frame. It grows in proportion to the
step, so it gets worse under timewarp. `IGroundTest` answers as a centre and a radius so that
sub-steps against one sample cost a subtraction; how often to re-sample is the caller's trade.
