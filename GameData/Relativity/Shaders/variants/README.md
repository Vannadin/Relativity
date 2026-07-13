# Doppler visual — pre-built shader variants (A/B test kit)

한 줄 요약: 아래 번들 중 하나를 `../relativityvisual.bundle` 로 덮어쓰고 KSP를 재시작하면 그 버전이 적용됩니다. DLL 재빌드·Unity 불필요.

Pre-built shader bundles for comparing the relativistic visual in-game **without rebuilding**. All were
built from `unity-shaders/variants/*.shader` (Unity 2019.4.18f1) and use the **same plugin uniforms**, so
swapping only needs a file copy — no `Relativity.dll` change.

## How to apply one

1. Copy the chosen `relativityvisual-vN-*.bundle` over the active bundle one folder up:
   `GameData/Relativity/Shaders/relativityvisual.bundle`
2. Launch KSP, fly to β ≳ 0.5 (the `Extras/relativity-torch.cfg` engine reaches it easily).
3. To go back to what was deployed, copy `relativityvisual-v0-current.bundle` back.

`relativity.cfg` knobs still apply to all of them: `dopplerIntensity`, `dopplerBeaming`,
`dopplerBeamMin/Max`, `dopplerAberration` (on/off for the cube variants; ignored by v1).

## The versions

Two families: **in-place** colour/beam (no cube — smooth, nothing lost/doubled, no star-bunching) and
**aberration** (cube — real star-bunching). Within in-place, the difference is the **colour model**.

**In-place (no cube) — the smooth looks, safest:**

| bundle | colour model | notes |
|--------|-------------|-------|
| **tint-bright** | early blue/red **tint** + **original aggressive beaming** (pow(D,4), clamp[0.05,12], no soft-clip) | Closest to the **very first build** you said "worked well". Punchy; forward saturates to white at high β (original behaviour). |
| **tint-smooth** | early blue/red **tint** + smooth bounded beaming | The first-build colour, but the forward no longer blows to white. If the "earlier good one" was the tint look, this is the clean version of it. |
| **v1-inplace** ⭐ | **blackbody** (Wien) colour + smooth beaming | The later smooth look (≈ the good screenshot). Recolours white stars via colour temperature. |

All three in-place bundles: ship depth-masked, planets & plumes preserved, **no double / no snap / no
camera-follow bug**. They differ only in colour feel — compare tint vs blackbody and pick.

**Aberration (cube) — real star-bunching (has the harder edge cases):**

| bundle | what it is | pros / cons |
|--------|-----------|-------------|
| **v0-current** | cube **pure-replace**, near camera, **no** Y-flip. | The state you last saw (sky pans opposite; planets/plumes replaced). Baseline. |
| **v2-aberration-replace** | pure-replace **+ DX11 Y-flip fix**. | Should now follow the camera. Planets/sun/plumes in the sky region are replaced (ship preserved). |
| **v3-aberration-additive** | `(src − cube) + shifted cube` **+ Y-flip**. | Star-bunching **and** kept objects if the cube lines up; may double if it doesn't. |

Suggested order: **tint-bright** and **tint-smooth** first (is *that* the earlier look you liked?), then
**v1-inplace** (blackbody vs tint colour), then the aberration set **v2 → v3** if you want star-bunching.
Tell me which one it was and I'll make it the shipped shader and refine from there.

> Housekeeping: this `variants/` folder is a dev A/B kit — delete it before any public release (the plugin
> only ever loads `Shaders/relativityvisual.bundle`, so these extra bundles are inert but shouldn't ship).
