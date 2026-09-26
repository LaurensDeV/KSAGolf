# Seeing the game from a terminal

How an agent working from WSL checks what this mod draws. Blender has an MCP connector that lets an
agent look at the open document, move a camera and render; KSA has nothing of the kind, so this is
one built out of what the engine and the mod allow.

## What is built

**The bridge** (`Ksa/Bridge.cs`, `Sim/BridgeCommand.cs`) and **the MCP server over it**
(`tools/ksa-mcp/server.py`, registered in `.mcp.json`), with **capture bundles**, **same-instant
variants**, **frame series** and **the toolkit** (`tools/vis/vis.py`). From a shell, before a session
has the MCP tools:

```bash
python3 tools/ksa-mcp/server.py cli launch '{"save":"<save>"}'
python3 tools/ksa-mcp/server.py cli pause
python3 tools/ksa-mcp/server.py cli frame '{"east":6,"north":0,"up":2,"own":true}'
python3 tools/ksa-mcp/server.py cli kitten_pose '{"bone":"Elbow_R","z":1,"deg":45}'
python3 tools/ksa-mcp/server.py cli capture '{"label":"pose","frames":6,"every_s":0.25}'
python3 tools/ksa-mcp/server.py cli capture '{"label":"ab","variants":[{"<Config field>":0}]}'
```

The CLI writes what an MCP client would be shown inline to `tools/ksa-mcp/last/`, and every call
empties it first — copy a capture out before the next call.

Both the bridge and the button below run on a **developer's install** only (`Build.Developer`, the
`developer` file `tools/deploy.sh` writes).

## The bridge

- **The mod** polls `<user dir>/Logs/bridge/in/` ten times a second, runs each command file it finds,
  oldest first, and answers in `.../out/` — JSON, plus any pictures. Commands: *status*, *pause*,
  *resume*, *speed*, *step* (run so many simulated seconds, then pause), *frame* (hold the view for a
  composed shot relative to the flown craft, or *release*), *orbit_place* (put a craft in a circular
  orbit, or beside another), *kitten_pose* and *kitten_prop* (`docs/KITTEN-ARMS.md`), *capture*,
  *set* and *get* (a `Config` field by name), *player_capture* and *load* (a save). The log is read by
  the server directly.
- **The MCP server** turns those into tools an agent calls the way it calls Blender's. It writes the
  command, waits for the result, and hands back pictures **inline, downscaled**, so a capture arrives
  in the conversation without a separate read.
- **Files, not a socket.** A folder in the user dir needs no port and no firewall, opens no listener,
  and works across the WSL boundary as it is.
- **The session is the player's.** The server launches a game only if none is running and never
  closes one it did not launch.

What it costs: the poll is a directory listing a few times a second; commands run in the mod's own
step, so nothing new happens inside the engine's render loop. What it buys: a question answers in
the time the thing takes — a paused capture in a second — rather than a relaunch.

C# still needs a relaunch: an assembly cannot be unloaded, and KSA compiles a mod's shader only at
load.

## Captures

**Bundles.** Every capture is `out/<command id>/<seq>-<label>.png` and a JSON sidecar: the camera's
field of view, **where the flown craft falls on screen**, the world's speed and pause, and the
settings that shape the picture. Names cannot collide — the mod renames KSA's file the moment it
appears and does not ask for the next capture until it has. KSA names screenshots to the second
(`ksa_yyyyMMdd_HHmmss_WxH.png`), so without that two captures in one second are one file.

**Crops come from the manifest**, projected by the same camera that took the picture, never from a
coordinate read off a picture and typed in.

**Controls taken in the same instant.** *capture* takes a list of variants — a `Config` field at
several values — pauses, photographs each, and returns the pictures and their diffs together. With
the world stopped, the weather, the sun and the camera are identical, so **the diff is the change and
nothing else**. A diff between two runs is mostly not the change: clouds move and the sun sets.

**Frame series.** Captures are spaced in simulated time (`every_s`), or in rendered frames while
paused (`every_frames`), so slowing the world makes any capture rate free. A series comes back as a
contact sheet, an animation, and a **temporal map** — a per-pixel standard deviation over
consecutive frames. Flicker, crawl and ghosting light up in it; a still cannot show them. A paused
series is blind to anything that needs the camera or the world moving, so measure flicker at a crawl,
never paused.

**Capture for Claude** is a button in the panel. It writes `out/player-<time>/`: a note of the
state, every `Config` field, the log's last lines, and consecutive frames with their manifests, so a
report from play arrives with the frames it is about. `ksa_player_captures` reads the newest back as
the state, a sheet and the temporal map. It writes a folder and sends nothing.

**The toolkit** (`tools/vis/vis.py`): contact sheets labelled from manifests, crops round the
projected craft, same-instant diffs, the grain measure (high-pass energy in a region), the temporal
map, colour at named points, and animation assembly.

## Engine facts the loop runs into

- **KSA's shader hot-reloader cannot serve a mod.** `KSA.AssetReloader.ShaderReloader` maps a path by
  searching for `Content`, which a mod's path does not contain — that is the
  `Error resolving include path … startIndex ('-1')` line in every session's log, and it is harmless.
  What it does is small: `ShaderReference.DoLoad()` recompiles the module. `DoLoad` is internal, and
  it destroys the old module even when the new compile returns nothing, as it does for a missing
  file; a compile error throws before anything is destroyed.
- **A write from WSL can reach the game late**: the game has been seen compiling a file as it was
  before a copy, one reload behind.
- **Weather cannot be switched off at runtime.** KSA reads its cloud setting when it builds the
  renderer, and its own settings screen treats the change as one needing a restart. Same-instant
  captures are what remove the drift a weatherless scene would.
- **The time of day is where the sun is from where the craft stands**, so night is had by moving the
  craft (*orbit_place*, or the panel's craft mover), not by a clock.

## What cannot be had

- **Headless rendering.** KSA ships Windows-only natives and runs its simulation through a Vulkan
  renderer; there is no headless mode, so a picture always means a running game.
- **A true video.** Nothing records the window, and the frame series is the substitute. It is also
  the better instrument: its frames are spaced in simulated time, so a slow machine takes the same
  shots as a fast one.
- **Unloading the DLL.** C# changes still need a relaunch.
