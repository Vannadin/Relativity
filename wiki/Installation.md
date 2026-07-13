# Installation

## Requirements

- **KSP 1.12.x** (built and tested against 1.12.5).
- **Harmony** (`Harmony2` / HarmonyKSP) — **required.** The resource, attitude, and background-thrust
  adapters all patch at runtime through Harmony.

## Install with CKAN (recommended)

Search for **Relativity** and install it — CKAN pulls in Harmony automatically. Optional integrations
(Kerbalism, RP-1) are picked up on their own if you already have them. (If it doesn't appear in CKAN
yet, indexing may still be in review — use the manual install below meanwhile.)

## Manual install

1. Download the release zip from the
   [Releases page](https://github.com/Vannadin/Relativity/releases).
2. Unzip it into your KSP install so the folder lands at `GameData/Relativity/`.
3. Install **Harmony** separately (from CKAN as `Harmony2`, or the HarmonyKSP release) so
   `GameData/000_Harmony/` exists.

Your `GameData` should contain both `Relativity/` and `000_Harmony/`.

The release includes the pre-built visual shader bundle at
`Relativity/Shaders/relativityvisual.bundle` — the optional relativistic [[Visuals]] need it. If
it is missing the visuals silently stay off (one `KSP.log` line) and the gameplay layer is
unaffected.

## Optional integrations (auto-detected)

None of these is required — each activates only if the target mod is present:

- **Kerbalism** — life-support consumption is dilated ×1/γ (radiation dose stays undilated).
- **RP-1** — a returning relativistic crew's retirement date is pushed forward by their time dilation.
- **Persistent Thrust**, life-support frameworks, warp mods, autopilots — see [[Compatibility]].

## Verifying it works

Launch a craft and open the [[Dashboard]] from the stock toolbar. In normal in-system flight the layer
is idle (you are far below the activation speed) — it comes alive only once you actually reach a
relativistic β, which needs a high-ΔV interstellar drive from another mod. To confirm the install loaded,
check `KSP.log` for the Relativity startup line, or use the diagnostics below.

## Diagnostics

- In flight, **LeftCtrl + LeftAlt + C** dumps an AddForce census to `KSP.log` (how the thrust correction
  is landing).
- Set `debugMode = true` in `GameData/Relativity/relativity.cfg` for per-engine detail. See
  [[Configuration]].

## Compatibility at a glance

Safe with **Principia** by design. Works with **Kerbalism 3.x / ROKerbalism**. Full mod-by-mod notes on
[[Compatibility]].
