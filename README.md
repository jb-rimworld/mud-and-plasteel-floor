# Mud and Plasteel Floor

Source: https://github.com/jb-rimworld/mud-and-plasteel-floor (MIT, see `LICENSE`).

A small RimWorld mod with two floors (`juliusberger00.mudandplasteelfloor`).

- **Mud**: builds on any walkable terrain for 1 hay per cell. Hostile pawns cross it at 40 % speed by default; your own colonists are not slowed. The enemy speed is a slider (1 % to 500 %), and a toggle makes mud slow everyone.
- **Plasteel floor**: a fine stone tile clad in plasteel (10 stone blocks + 5 plasteel per cell, Stonecutting, construction 6). 150 % movement speed, Cleanliness 1.2 and Beauty 5 by default, each with a slider. Counts as a fine floor for Royalty room requirements. One Architect button for all stone types.

Built and tested on RimWorld 1.7. The 1.6 build is the last 1.0 version, best effort and untested since.

Requires [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077). No other dependency.

## Layout

| Folder | What |
|---|---|
| `Common/` | defs, translations (English, German) and textures for 1.7 |
| `1.7/` | the 1.7 assembly; `1.7/Odyssey` holds the vacstone floor text |
| `1.6/` | frozen 1.6 state (`1.6/Root` + `1.6`), see `Source/Frozen16.sha256` |
| `Source/` | C# source |

## Build

```sh
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
dotnet build -c Release Source/MudAndPlasteelFloor/MudAndPlasteelFloor.csproj
```

## Licence

MIT, see `LICENSE`.
