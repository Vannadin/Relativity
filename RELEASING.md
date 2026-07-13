# Releasing Relativity

Maintainer runbook. **Two-repo flow** (since 2026-07-09): this private repo (`Vannadin/Relativity-dev`)
holds the full dev truth and builds the DLL (Windows — KSP `Managed/` isn't on the Mac box); the public
repo (**`Vannadin/Relativity`**) carries a curated snapshot history, the tags, the GitHub Releases, and
the wiki. CKAN/SpaceDock/AVC all point at the public repo.

## Version scheme

- **Git tag / GitHub release:** `vX.Y.Z` on the **public** repo only (pre-1.0 carried a `-beta` suffix
  and the `--prerelease` flag; 1.0+ are plain releases). The dev repo is not tagged.
- **KSP-AVC `.version`:** numeric `X.Y.Z` — AVC has no prerelease field.
- **CKAN:** reads the bundled `.version` via `$vref: #/ckan/ksp-avc`.
- Keep `src/Relativity.csproj <Version>`, `GameData/Relativity/Relativity.version`, and the CHANGELOG
  heading in sync on every bump. `.version`, `Relativity.netkan`, and the release notes must point at
  `Vannadin/Relativity` (they ship / get published; the dev-repo name would 404 for players).

## Per-release steps (Windows, from Relativity-dev)

1. **Bump + verify:** version sync as above, `dotnet build -c Release` 0/0, tests pass, CHANGELOG
   section finalized, `distribution/release-notes-X.Y.Z.md` written (derived from the CHANGELOG).

2. **Build + package:**
   ```powershell
   ./package.ps1 -Version X.Y.Z
   ```
   Verifies the DLL, stages a clean `GameData/Relativity` (drops `PluginData/`, caches, `*.pdb`,
   the `Shaders/variants/` dev kit; adds `LICENSE`), writes `bin/Relativity-X.Y.Z.zip` and prints the
   entry list — confirm `Plugins/Relativity.dll`, `Relativity.version`, `relativity.cfg`, `Icons/`,
   `Shaders/relativityvisual.bundle`, `LICENSE`.

3. **Export the public snapshot** (strips notes/AI docs, rewrites any leftover dev-repo URLs):
   ```bash
   ./scripts/export-public.sh <tmp>/public-export   # prints a leftover-reference check — must be none
   ```
   Also grep the export for scrubbed provenance terms before pushing (must be none).

4. **Snapshot commit + tag on the public repo:**
   ```powershell
   git clone https://github.com/Vannadin/Relativity.git <tmp>/public-repo
   cd <tmp>/public-repo; git rm -rq .; Copy-Item <tmp>/public-export/* . -Recurse -Force
   git add -A; git commit -m "Relativity vX.Y.Z - <headline>"
   git tag -a vX.Y.Z -m "Relativity vX.Y.Z"; git push origin main vX.Y.Z
   ```

5. **GitHub release** (needs `gh auth login` once per machine):
   ```powershell
   gh release create vX.Y.Z bin/Relativity-X.Y.Z.zip --repo Vannadin/Relativity `
     --title "vX.Y.Z — <headline>" --notes-file distribution/release-notes-X.Y.Z.md
   ```
   (`--prerelease` only for betas — AVC's `ALLOW_PRE_RELEASE:false` and CKAN skip those.)

6. **SpaceDock:** create/update the mod page (category *Gameplay*, KSP 1.12.5, MIT) with
   `distribution/spacedock.md`, upload the same zip. Optionally point `.version` `DOWNLOAD` at
   SpaceDock and add `resources.spacedock` to the netkan; commit.

7. **CKAN (first release only):** PR `Relativity.netkan` into
   [KSP-CKAN/NetKAN](https://github.com/KSP-CKAN/NetKAN) under `NetKAN/`. The bot indexes GitHub
   releases via `$kref` and reads versions from the bundled `.version` — future releases are picked up
   automatically; re-PR only if the netkan metadata itself changes.

## Notes

- The zip's root is `GameData/` so players unzip into the KSP install directory; CKAN auto-detects the
  layout with no explicit `install` stanza.
- DLLs stay out of git by policy (Take2 EULA); the release zip is the only place the assembly ships.
- The public wiki lives on the public repo (`Relativity.wiki.git`); push the `wiki/` pages there when
  they change (drop DRAFT banners first).
- Feature verification status per release lives in `CHANGELOG.md`; release notes are derived from it.
