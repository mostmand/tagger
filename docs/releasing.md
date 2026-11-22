# Releasing Tagger

Tagger ships **self-contained portable builds**: the .NET runtime and the audio
runtime are inside the archive, so users do not install .NET or VLC separately.

- Linux: `tagger-<version>-linux-x64.tar.gz`
- Windows: `tagger-<version>-win-x64.zip`

## Workflows

| Workflow | Trigger | What it does |
| --- | --- | --- |
| `.github/workflows/ci.yml` | push to `main`, pull requests, manual | Restores, builds, and runs the test suite on Ubuntu and Windows. |
| `.github/workflows/release.yml` | tag `v*`, manual | Builds the portable Linux and Windows archives and uploads them. A tag also creates a GitHub release with `SHA256SUMS.txt`. |

A manual run of the release workflow builds the same archives but does not create
a release, which is useful for smoke-testing packaging from a branch.

## Cutting a release

```bash
# 1. Update CHANGELOG.md and choose a version.
# 2. Tag and push. The tag is the version, without the leading "v".
git tag v0.2.0
git push origin v0.2.0
```

The release job runs after both platform jobs succeed. If a platform build fails,
no release is published, so the two archives always match.

To build artifacts without tagging, run the **Release** workflow manually with a
version such as `0.2.0-rc.1`.

## Reproducing a package locally

Linux (requires Docker on the first run to build the portable libVLC runtime):

```bash
build/libvlc-linux/build.sh
dotnet publish src/Tagger.Desktop/Tagger.Desktop.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:Version=0.2.0 -o publish/linux
```

Windows (PowerShell):

```powershell
dotnet publish src/Tagger.Desktop/Tagger.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:Version=0.2.0 -o publish/windows
```

The archives contain the app, `LICENSE`, both third-party notice files, and on
Linux the bundled libVLC tree (including `COPYING.vlc`).

## Portable builds or an installer?

**Start with portable builds.** They are the lowest-risk distribution channel and
match what early users of a tag editor expect: unzip, run, keep your own folder.

### Windows

Ship the portable `.zip` now. Add an installer when users ask for Start Menu
entries, file associations, or "open with" integration:

- **Inno Setup** or **WiX** produce a small, conventional `.exe`/`.msi` and can
  register associations. This is the simplest upgrade from a portable folder.
- **MSIX** gives clean install/uninstall and update plumbing, but package identity,
  file-type activation, and access to library folders add friction for a local-first
  editor. Choose it only if you need Store distribution or MSIX auto-update.
- A portable build is **not** a substitute for signing. Unsigned binaries trigger
  SmartScreen on first run whether or not there is an installer. Use
  **Azure Trusted Signing** (CI-friendly, no hardware token) or an OV/EV certificate
  and sign every published `.exe`/`.dll`.

When the app is stable, publish a **winget** manifest so users can
`winget install tagger`; winget can install both portable and installer builds.

### Linux

- **AppImage** is the natural next step: one executable file, no root, works across
  distributions. It matches the portable archive and can be built in the same job.
- **`.deb`** is worth adding for Debian/Ubuntu users who prefer their package
  manager. Keep it per-architecture (`amd64` first).
- **Flatpak** is a larger commitment. The sandbox is good for safety, but a tag
  editor needs broad access to a music library; the user must grant folder access
  through portals, and VLC/MPRIS integration needs extra work. Add it only if you
  want Flathub distribution.
- The portable build targets x86_64 with glibc 2.35+ (Ubuntu 22.04 baseline). The
  host must provide the usual desktop stack (`libX11`, `fontconfig`, etc.). musl
  distributions are not supported by the bundled libVLC runtime.

### macOS

Not built yet. Plan for a universal, signed, notarized `.dmg`; unsigned apps are
blocked by Gatekeeper. This needs an Apple Developer account and a macOS runner.

## Auto-updates

Do not add a background updater while Tagger is a preview. When you are ready:

- **Velopack** fits this app well: Windows, Linux, and macOS, delta updates, and
  installer generation from CI.
- Keep settings, the undo journal, and any cache in the platform config directory
  (Tagger already does), never inside the install folder. That makes both portable
  and installed builds safe to update.
- Portable users should update by replacing the folder. Never silently rewrite a
  portable install the user placed somewhere deliberate.

## Release hygiene

- Sign release artifacts and publish `SHA256SUMS.txt` (the workflow does the checksums).
- Pin third-party actions to a commit SHA once the workflow stabilizes, or enable
  Dependabot for `github-actions`.
- Keep the libVLC bundle pinned. The Linux cache key includes the Dockerfile and
  collector, so changing the VLC version invalidates the cache automatically.
- Verify the GPL/LGPL obligations for the bundled VLC runtime and keep
  `build/libvlc-linux/README.md` current with the source-offer information.
- Do not attach debug symbols or `TestResults` to public releases.
