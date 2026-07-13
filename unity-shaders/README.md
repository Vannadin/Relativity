# Doppler visual — shader bundle build

The relativistic Doppler + beaming screen-space effect ([`docs/design.md`](../docs/design.md) §2.5,
Tier 1) is a GPU post-process shader. **KSP cannot compile ShaderLab at runtime**, so the shader must
be pre-compiled into an AssetBundle with the game's Unity version and shipped in `GameData/`. This
folder is the Unity project that produces that bundle. It is **dev tooling** — it is not part of the
plugin build and is excluded from the public release export.

The runtime side (loading the bundle, attaching the effect to the camera, feeding β/γ per frame) is
`src/DopplerVisual.cs` in the plugin — it does **not** need this Unity project, only the built bundle.

## What you need

- **Unity 2019.4.18f1 LTS** — the exact version KSP 1.12.x runs on. (Older 2019.x can load, but match
  the runtime to be safe.)

## Steps

1. Open this folder (`unity-shaders/`) as a Unity project in 2019.4.18f1.
2. **Enable OpenGLCore** so one Windows bundle serves both DX11 and OpenGL players:
   *Project Settings ▸ Player ▸ Other Settings ▸ Graphics APIs for Windows* → add **OpenGLCore**
   (keep Direct3D11 first). This is the single most common source of "pink shader" reports if skipped.
3. Build the bundle — either:
   - **Menu:** *Relativity ▸ Build Doppler Bundle*, or
   - **Headless:**
     ```
     "<Unity>/Editor/Unity.exe" -batchmode -quit -projectPath "<path>/unity-shaders" -executeMethod BuildDopplerBundle.Build
     ```
4. The output is `unity-shaders/AssetBundles/relativityvisual.bundle`. Copy it to:
   ```
   GameData/Relativity/Shaders/relativityvisual.bundle
   ```
   (`src/DopplerVisual.cs` loads exactly that path.)

## Notes

- The bundle file uses a **neutral `.bundle` extension** on purpose: it is loaded by explicit path via
  `AssetBundle.LoadFromFile`, so neither KSP's `GameDatabase` nor Shabby touches it. No Shabby
  dependency — the mod stays MIT and dependency-free for visuals.
- Editing the shader (`Assets/Relativity/DopplerVisual.shader`) → rebuild the bundle → re-copy. The
  plugin DLL does not need rebuilding for a shader-only change.
