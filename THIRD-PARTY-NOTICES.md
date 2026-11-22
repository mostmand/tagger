# Third-party notices

Tagger ships audio playback through LibVLCSharp and libVLC. This file summarizes the
licenses of the components that are redistributed with the desktop app. It is not legal
advice; review the upstream license texts before distributing a release.

## Managed and native packages

| Component | Version | License | Notes |
| --- | --- | --- | --- |
| [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp) | 3.10.2 | MIT | Managed .NET bindings. |
| [VideoLAN.LibVLC.Windows](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows) | 3.0.24 | VLC is GPL-2.0-or-later; some bundled libraries are LGPL-2.1-or-later | Official Windows native runtime. |
| [VideoLAN.LibVLC.Mac](https://www.nuget.org/packages/VideoLAN.LibVLC.Mac) | 3.1.3.2 | VLC is GPL-2.0-or-later; some bundled frameworks are LGPL-2.1-or-later | Official macOS native runtime. |

The `VideoLAN.LibVLC.*` packages include their own license texts in the package contents.
Those texts take precedence over this summary.

## Linux runtime

Linux builds bundle a portable runtime produced by
[`build/libvlc-linux/build.sh`](build/libvlc-linux/build.sh). It is assembled from pinned
Ubuntu 22.04 packages:

- `libvlc5` and `vlc-plugin-base` **3.0.16-1build7**, and their transitive shared-library
  closure.
- Video and GUI plugins are intentionally omitted; Tagger only plays audio.
- The generated runtime includes `COPYING.vlc` from the Ubuntu `vlc-plugin-base` package.

VLC is distributed under the GNU General Public License version 2 or later
(`GPL-2.0-or-later`). Some bundled libraries, including FFmpeg components, are under the
GNU Lesser General Public License (`LGPL-2.1-or-later`), while optional codec components
such as x264 are under `GPL-2.0-or-later`. The Ubuntu package copyright file contains the
complete per-component list.

Because the runtime redistributes GPL-covered binaries, a release that ships it must also
make the corresponding source available. The `Dockerfile` and `collect.py` in
`build/libvlc-linux` pin the versions and reproduce the bundle from the public Ubuntu
archive; the source packages for those exact versions are available from
`archive.ubuntu.com`. Rebuilding with that script is the supported way to reproduce the
shipped binaries.

## What Tagger does not bundle

- No VLC installation is required on Windows or macOS; the official NuGet packages are
  copied next to the app.
- On Linux, `dotnet run` can fall back to a development machine's system libVLC if the
  bundled runtime is absent. Release builds are expected to ship the bundle.
- Tagger itself remains under the repository's own license; the third-party components
  above keep their respective licenses.
