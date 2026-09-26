# KSAGolf

Golf for **Kitten Space Agency** (RocketWerkz): clubs a kitten holds, a ball that
flies through whatever air the planet has, and later golf carts and minigolf courses.

> **Nothing to play yet.** This is the foundation the golf is built on — the mod loads, opens a
> panel and carries the developer tools — and no club, ball, cart or course exists yet. A release
> from this state would change nothing a player can see.

KSA has no official code-modding API; this uses the community
[StarMap](https://github.com/StarMapLoader/StarMap) loader and is built against KSA build
`2026.9.22.5482`. A different build may need a rebuild of the mod.

## Install

You need **Kitten Space Agency** and **[StarMap](https://github.com/StarMapLoader/StarMap/releases)**.
Edit StarMap's `StarMapConfig.json` to point at your KSA install; StarMap reads that file **relative
to its own directory**, so launch it from where it lives. **Windows and Linux both work**: the mod is
a portable .NET assembly, so one archive covers both.

1. **Get the mod.** Download `KSAGolf-<version>.zip` from [Releases](../../releases), or build it
   with `./tools/package.sh`.

2. **Unzip it into the `mods` folder** inside KSA's user directory, so you end up with
   `<KSA user directory>/mods/KSAGolf/KSAGolf.dll` beside `mod.toml`:

   | Platform | KSA user directory |
   | --- | --- |
   | Windows | `Documents\My Games\Kitten Space Agency\` |
   | Linux | wherever KSA keeps its user data, commonly `~/.local/share/Kitten Space Agency/`; the folder containing `manifest.toml` and `Logs/` |
   | Proton / Wine | inside the prefix, at `.../drive_c/users/steamuser/Documents/My Games/Kitten Space Agency/` |

   On Linux the **case** of every file and folder matters, so unzip rather than retyping names.

3. **Register it in `manifest.toml`**, in the same user directory. Dropping the folder in is not
   enough — KSA discovers mods through this list, and StarMap walks the same list:

   ```toml
   [[mods]]
   id = "KSAGolf"
   enabled = true
   ```

4. **Launch through StarMap**, not the game directly: `StarMap.exe` on Windows, `dotnet StarMap.dll`
   from the same folder on Linux.

### Check it worked

The mod writes its own log to `Logs/KSAGolf.log` under the KSA user directory, with the session
before kept as `KSAGolf.prev.log`. You should see `loading (mod id: KSAGolf)`, then about twenty
seconds later, once the game has finished loading, `ready - open the 'KSAGolf' panel`. The panel is
under **KSAGolf** in the menu bar, or behind the floating **KSAGolf** button.

| Symptom | Cause |
| --- | --- |
| No `KSAGolf.log` at all | StarMap never ran the mod: check the `manifest.toml` entry, and that you launched StarMap. |
| A log, but no panel | Check `mod.toml`'s `EntryAssembly = "KSAGolf"` matches the DLL name, and read the log. |

**Verbose log** on the panel puts the detail a bug report wants into that log.

## Build

```bash
./tools/doctor.sh      # can this machine build, test and run it?
./tools/build.sh       # build
./tools/test.sh        # the headless tests; needs KSA's assemblies, not the game
./tools/deploy.sh      # build, install into your KSA mods folder and register it
./tools/run.sh         # ...and launch the game through StarMap, following the log
./tools/check-all.sh   # everything CI runs
```

Building needs KSA's own assemblies. They are RocketWerkz's copyrighted files and are **not** in this
repository: `./tools/sync-import.sh` copies them from your install into `Import/`, and the build also
finds a game install on its own. `CONTRIBUTING.md` has the rest, and `CLAUDE.md` is the reference for
how the code is laid out and why.

## Licence

MIT — see [LICENSE](LICENSE). Kitten Space Agency and its assemblies are RocketWerkz's.
