# Releasing Relativity

Maintainer runbook. The compiled `Relativity.dll` is built on Windows (KSP `Managed/` isn't present on
the Mac dev box), so packaging and upload happen on Windows; metadata and docs are prepared in-repo.

## Version scheme

- **Git tag / GitHub release:** `v0.1.0-beta` (human-facing; pre-1.0 = beta).
- **KSP-AVC `.version`:** numeric `0.1.0` — AVC has no prerelease field.
- **CKAN:** reads the bundled `.version` via `$vref: #/ckan/ksp-avc`, so its version is `0.1.0`.
- Keep `src/Relativity.csproj <Version>`, `GameData/Relativity/Relativity.version`, and the CHANGELOG
  heading in sync on every bump.

## One-time setup (first release only)

1. **Make the repo public:** `gh repo edit Vannadin/Relativity --visibility public`
   (it starts private). CKAN and SpaceDock both need a reachable public repo.
2. Confirm **Harmony2** is the correct CKAN identifier for HarmonyKSP (it is, as of 2026).
3. Double-check optional-dep CKAN identifiers before the netkan PR: `Kerbalism`, `RP-1`,
   `ModuleManager`. CKAN's CI validates these, but catching typos early saves a round-trip.

## Per-release steps (Windows)

1. **Build + package** (one script):
   ```powershell
   ./package.ps1                 # defaults to Version = 0.1.0-beta
   ```
   This runs `dotnet build -c Release`, verifies `GameData/Relativity/Plugins/Relativity.dll`, stages a
   clean `GameData/Relativity` (drops `PluginData/`, `*.ConfigCache`, `*.pdb`, `.DS_Store`; adds a copy
   of `LICENSE`), and writes `bin/Relativity-0.1.0-beta.zip`. It prints the zip's file list — confirm it
   contains `GameData/Relativity/Plugins/Relativity.dll`, `Relativity.version`, `relativity.cfg`,
   `Icons/`, and `LICENSE`.

2. **Tag + GitHub release:**
   ```powershell
   git tag -a v0.1.0-beta -m "Relativity v0.1.0-beta — first public beta"
   git push origin v0.1.0-beta
   gh release create v0.1.0-beta bin/Relativity-0.1.0-beta.zip `
     --title "v0.1.0-beta — first public beta" `
     --notes-file distribution/release-notes-0.1.0-beta.md `
     --prerelease
   ```
   Mark it `--prerelease` so AVC's `ALLOW_PRE_RELEASE:false` and CKAN skip it until you're ready to
   promote; drop the flag (or edit the release) when it should go live.

3. **SpaceDock:** create the mod page (category *Gameplay*, KSP 1.12.5, license MIT), paste the text from
   `distribution/spacedock.md`, and upload the same zip. Then:
   - add the SpaceDock URL to `Relativity.netkan` `resources.spacedock`,
   - point `GameData/Relativity/Relativity.version` `DOWNLOAD` at the SpaceDock download if you prefer it
     over the GitHub releases page,
   - commit those two edits.

4. **CKAN:** open a PR adding `Relativity.netkan` to
   [KSP-CKAN/NetKAN](https://github.com/KSP-CKAN/NetKAN) under `NetKAN/`. The bot indexes the GitHub
   release via `$kref` and reads the version from the bundled `.version`. Once merged, CKAN auto-picks up
   future releases — you only re-PR if the netkan metadata itself changes.

## Notes

- The zip's root is `GameData/` so players unzip into the KSP install directory; CKAN auto-detects the
  `GameData/Relativity` layout with no explicit `install` stanza.
- DLLs stay out of git by policy (Take2 EULA — see `.gitignore`); the release zip is the only place the
  compiled assembly ships.
- Feature verification status per release lives in `CHANGELOG.md`; the release notes are derived from it.
