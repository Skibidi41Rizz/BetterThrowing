# Changelog

## 1.9.1

- Default `AssistLevel` is now `4` (maximum).

## 1.9.0

- Renamed the mod to **BetterThrowing** for the public release.
- Config entries now have clear English names and descriptions.
- Hand-collision ignore now runs on real time (0.25 s) and ends as soon as your hand leaves the object, so weapons can be grabbed again quickly, including in slow motion.
- New `HeadChance` (25%): with `AimAtHead` off, some throws still aim at the head.

## 1.8.2

- Tip alignment has its own speed gate (`TipMinSpeed`, 6 m/s): releasing a spear to regrip no longer snaps its pose.

## 1.8.1

- Any item whose name contains `spear` is now recognised, including `Short Spear` variants that differ from the UI name.
- Tip is the farthest collider point from the hand; removed the length/ratio gate for spears.

## 1.8.0

- Tip-first behaviour is limited to `Combat knife`, `Short Spear` and `Long Spear`; other weapons (katana, medieval sword, naginata, shovel, pitchfork) throw as before.
- Alignment only happens on a real throw, not when you simply let go to regrip.
- Spear tip is the farthest collider point from the hand; knife tip likewise.

## 1.7.0

- Spear/knife alignment now happens on every throw, not only when a target is found.

## 1.6.0

- Knife stabilisation: knives and daggers are detected by geometry, rotated tip-first and held with a stronger angular controller.
- New `KnifeAssist` toggle.

## 1.5.1

- New `AssistRagdolls` toggle: thrown enemies and dummies can keep the throw impulse or use normal physics.

## 1.5.0

- Spears without a `MeleeWeaponWrapper` (everything except the Naginata) are now recognised by geometry and fly tip-first.
- Tip lock extended to 2.5 s.

## 1.4.0

- `AimAtHead` is the only head/body switch; no auto-detection of sharp weapons.
- Aim point is chest (belly fallback), never arms or legs.
- Every throw uses the straight-arc solver; `SharpArc` renamed to `Arc`.
- Faster presets and new level 5.

## 1.3.0

- Sharp weapons aim at the body instead of the head.
- Straight-line solver for sharp weapons (`SharpArc`).

## 1.2.0

- Assist levels 1-4.
- Hand-speed throw detection, 3-frame velocity re-assert, air-drag compensation.
- Spear tip lock.

## 1.1.0

- Weak normal-time throws fixed with `MinAimSpeed`.
- Hand collision ignore to stop the weapon bouncing off the hand.
- Blade-first alignment for sharp weapons.

## 1.0.0

- First working throw assist: target picking, ballistic solve, one velocity at release.
