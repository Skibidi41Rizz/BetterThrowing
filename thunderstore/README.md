# BetterThrowing

Throw assist for **BRUTALISTICK VR**. Vanilla throwing is pure physics, so weak arm motion means a weak throw and spears tumble in the air. BetterThrowing reads the throw as it leaves your hand, picks the enemy you are throwing at, and gives the object a correct ballistic velocity for that target.

## Features

- **Aim correction** — picks the target closest to your throw direction and solves the exact velocity to hit it, including air drag.
- **Weak throws still reach** — if the game gives the object a weak release velocity, the speed of your hand is used instead, so normal-time throws fly properly.
- **Flat, repeatable arcs** — throws may rise only a few degrees above the straight line to the target, so spears fly where you point them.
- **Spears and knives tip-first** — `Combat knife`, `Short Spear` and `Long Spear` snap to a point-first pose on release and are held that way in flight (2.5 s lock, tumble suppressed).
- **No hand bounce** — the thrown object ignores the player's hand colliders for the first frames, then everything is restored.
- **Thrown enemies** — grabbed enemies and dummies also get the throw impulse (can be turned off).
- **Configurable aggression** — five presets from light to extreme, or full manual control.

Makes no changes to game files and does not touch other players: it is a single-player assist.

## Installation

1. Install [MelonLoader 0.7.3](https://github.com/LavaGang/MelonLoader) for BRUTALISTICK VR.
2. Drop `BetterThrowing.dll` into the `Mods` folder next to `BrutalistickVR.exe`.
3. Start the game. The MelonLoader console should print `BetterThrowing 1.9.1 загружен.`

Thunderstore mod managers install the DLL automatically.

## Configuration

Settings live in `UserData/MelonPreferences.cfg` (section `[BetterThrowing]`) and can be edited in game with ModMenu.

| Key | Default | Meaning |
| --- | --- | --- |
| `Enabled` | `true` | Turn the assist on or off. |
| `AssistLevel` | `4` | `1` light, `2` medium, `3` strong, `4` maximum, `5` extreme, `0` manual. |
| `Strength` | `1.0` | Manual mode only. `1` = full correction, `0.5` = halfway. |
| `MaxAngle` | `35` | Manual mode only. Max angle between throw direction and target, degrees. |
| `MaxDistance` | `20` | Max target distance, metres. |
| `MinThrowSpeed` | `3` | Slower body speed is not a throw. |
| `HandThrowSpeed` | `2.5` | Hand speed that still counts as a throw when the body got a weak velocity. |
| `TipMinSpeed` | `6` | Slower releases are regrips: spear/knife pose is not changed. |
| `HeadChance` | `25` | Head chance, %, when `AimAtHead` is off. |
| `MinAimSpeed` | `8` | Manual mode only. Weak throws fly at least at this horizontal speed. |
| `MaxSpeed` | `30` | Max speed the mod is willing to give a throw. |
| `AimAtHead` | `true` | Aim at the head; otherwise chest, or belly. Never arms or legs. |
| `BladeFirst` | `true` | Spears and knives fly tip-first. |
| `Arc` | `8` | Max rise above the straight line to the target, degrees. `0` = as flat as possible. |
| `IgnoreUnconscious` | `false` | Do not assist throws at unconscious enemies. |
| `AssistRagdolls` | `true` | Give the throw impulse to grabbed enemies and dummies. |
| `KnifeAssist` | `true` | Stabilise knives separately from spears. |
| `Log` | `false` | Print a line per assisted throw to the MelonLoader console. |

### Assist levels

| Level | Aim cone | Speed |
| --- | --- | --- |
| 1 | 25° | 10 m/s |
| 2 | 35° | 13 m/s |
| 3 | 50° | 17 m/s |
| 4 | 70° | 22 m/s |
| 5 | 85° | 27 m/s |

## Notes

- Targets are ragdoll characters (`StickHealth`), not props. The player's own body is ignored.
- The solver never touches the object after the throw: one velocity is applied at release, plus a short tip lock for spears and knives.
- Built against the game's IL2CPP assemblies and HurricaneVR framework; no game assets are included.

## Build from source

```
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\BRUTALISTICK VR"
```

The project references `MelonLoader/net6` and `MelonLoader/Il2CppAssemblies` from the game folder. To build the Thunderstore zip run `.\package.ps1`.

## Credits

- Kubunautilus — BRUTALISTICK VR.
- LavaGang — MelonLoader.
- HurricaneVR — grab framework used by the game.
