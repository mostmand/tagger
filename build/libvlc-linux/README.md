# Portable libvlc runtime for Linux

`Tagger.Desktop` uses LibVLCSharp for audio playback. Windows and macOS get
their native libraries from the official `VideoLAN.LibVLC.*` NuGet packages.
VideoLAN does not publish a Linux native package, so releases bundle a portable
runtime built by this directory.

## Build

```bash
build/libvlc-linux/build.sh
```

The script builds an Ubuntu 22.04 container, installs `libvlc5` and
`vlc-plugin-base`, and copies the transitive shared-library closure and audio
plugins into `src/Tagger.Desktop/libvlc/linux-x64`. It also writes a compressed
archive to `artifacts/`.

The runtime is intentionally not committed. Run the script once on a development
machine (Docker required) before `dotnet run` on Linux, or let the release
pipeline run it and ship the directory next to the app.

## Why a separate runtime

LibVLCSharp on Linux loads `libvlc` through the normal dynamic loader, and it
does not support a directory argument there. Bundling a whole runtime on
`LD_LIBRARY_PATH` would also shadow the host's ICU, OpenSSL, X11, and systemd
libraries, which .NET and Avalonia need.

Instead the bundle is self-isolating:

* every bundled object gets an `$ORIGIN` RPATH, so VLC's own dependency chain
  resolves inside `lib/` without affecting other libraries in the process;
* `bin/` contains only `libvlc`/`libvlccore` entry points;
* the app calls `dlopen` on `lib/libvlc.so` and binds LibVLCSharp's imports to
  that handle, so `dotnet run` works without `LD_LIBRARY_PATH` or a system VLC.

## Compatibility and licensing

* Architecture: x86_64 (`linux-x64`). The collector pins the x86_64 Ubuntu paths.
* Baseline: Ubuntu 22.04 / glibc 2.35 or newer (Debian 12, Ubuntu 22.04+,
  Fedora 36+, Arch). musl distributions are not supported by this bundle.
* Video and GUI plugins are omitted because Tagger only plays audio. This keeps
  the bundle around 130 MB extracted and avoids a second X11/GL stack.
* The bundle contains VLC 3.0.16 and its dependencies under their respective
  licenses (VLC: GPL-2.0-or-later, most libraries: LGPL). `COPYING.vlc` is
  included in the generated runtime. Source packages are available from the
  Ubuntu archive under the versions pinned in `Dockerfile`; rebuild with this
  script to reproduce the binaries.
