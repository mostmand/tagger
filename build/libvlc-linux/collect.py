#!/usr/bin/env python3
"""Collect a portable libvlc runtime from an installed Ubuntu VLC package tree.

The container installs the VLC runtime packages into /usr. This script walks the
libvlc shared objects and every retained plugin, resolves their ELF dependencies
with `ldd`, and copies the closure into a flat `lib` directory plus the plugin
tree.

Two details make the bundle safe to drop into an existing desktop process:

* Every bundled object gets an `$ORIGIN` RPATH, so VLC's dependency chain finds
  its own libraries without a process-wide LD_LIBRARY_PATH that would otherwise
  shadow the host's ICU, OpenSSL, X11, or systemd libraries.
* `bin` contains only `libvlc*`/`libvlccore*` links. If the loader path is used
  at all, it can only affect the two VLC entry points.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys

LIB_DIRS = ("/usr/lib/x86_64-linux-gnu", "/lib/x86_64-linux-gnu")
PLUGIN_ROOT = "/usr/lib/x86_64-linux-gnu/vlc/plugins"
PLUGIN_ENTRY_POINTS = ("libvlc", "libvlccore")
# Audio playback only: these pull in a second X11/GL/font stack or a GUI toolkit.
SKIPPED_PLUGIN_DIRS = {
    "gui",
    "visualization",
    "video_filter",
    "video_output",
    "video_splitter",
    "text_renderer",
}
# Provided by the host. Bundling the C runtime or the graphics stack makes a
# desktop process load two competing copies of libraries it already uses.
HOST_LIBS = {
    "ld-linux-x86-64.so.2",
    "libc.so.6",
    "libm.so.6",
    "libmvec.so.1",
    "libpthread.so.0",
    "libdl.so.2",
    "librt.so.1",
    "libresolv.so.2",
    "libutil.so.1",
    "libanl.so.1",
    "libstdc++.so.6",
    "libgcc_s.so.1",
}
HOST_LIB_PREFIXES = (
    "libX11",
    "libxcb",
    "libXext",
    "libXrender",
    "libXi.",
    "libXfixes",
    "libXcursor",
    "libXrandr",
    "libXinerama",
    "libXau.",
    "libXdmcp",
    "libwayland-",
    "libGL",
    "libEGL",
    "libgbm",
    "libdrm",
    "libfontconfig",
    "libfreetype",
    "libharfbuzz",
    "libcairo",
    "libpixman",
    "libpng",
    "libicu",
    "libssl",
    "libcrypto",
    "libsystemd",
    "libudev",
    "libdbus-1",
    "libselinux",
    "libapparmor",
    "libz.so",
    "libzstd",
)


def is_elf(path: str) -> bool:
    try:
        with open(path, "rb") as handle:
            return handle.read(4) == b"\x7fELF"
    except OSError:
        return False


def run_tool(args: list[str]) -> str:
    try:
        return subprocess.run(
            args, check=False, capture_output=True, text=True, timeout=60
        ).stdout
    except (OSError, subprocess.TimeoutExpired):
        return ""


def dependencies(path: str) -> list[str]:
    found = []
    for line in run_tool(["ldd", path]).splitlines():
        line = line.strip()
        if "=>" in line:
            target = line.split("=>", 1)[1].strip().split(" ")[0]
        elif line.startswith("/"):
            target = line.split(" ")[0]
        else:
            continue
        if target.startswith("/") and os.path.isfile(target):
            found.append(os.path.realpath(target))
    return found


def soname(path: str) -> str | None:
    for line in run_tool(["readelf", "-d", path]).splitlines():
        if "(SONAME)" in line and "[" in line and "]" in line:
            return line.split("[", 1)[1].split("]", 1)[0]
    return None


def is_host_lib(name: str) -> bool:
    return name in HOST_LIBS or name.startswith(HOST_LIB_PREFIXES)


def walk_plugins(root: str) -> list[str]:
    files = []
    for directory, _dirs, names in os.walk(root):
        relative = os.path.relpath(directory, root)
        if relative.split(os.sep)[0] in SKIPPED_PLUGIN_DIRS:
            continue
        for name in names:
            candidate = os.path.join(directory, name)
            if os.path.isfile(candidate) and is_elf(candidate):
                files.append(candidate)
    return files


def set_rpath(path: str, rpath: str) -> None:
    subprocess.run(
        ["patchelf", "--force-rpath", "--set-rpath", rpath, path],
        check=True,
        capture_output=True,
        timeout=60,
    )


def link(lib_dir: str, name: str) -> bool:
    source = os.path.join(lib_dir, name)
    if not os.path.isfile(source):
        return False
    for alias in {name, soname(source)} - {None}:
        target = os.path.join(lib_dir, alias)
        if not os.path.lexists(target):
            os.symlink(name, target)
    return True


def main() -> int:
    out = sys.argv[1]
    lib_out = os.path.join(out, "lib")
    bin_out = os.path.join(out, "bin")
    plugin_out = os.path.join(out, "plugins")
    for directory in (lib_out, bin_out, plugin_out):
        os.makedirs(directory, exist_ok=True)

    roots = []
    for lib_dir in LIB_DIRS:
        for name in os.listdir(lib_dir):
            if name.startswith(("libvlc.so", "libvlccore.so")):
                roots.append(os.path.realpath(os.path.join(lib_dir, name)))
    plugins = walk_plugins(PLUGIN_ROOT)
    if not roots or not plugins:
        print("missing libvlc or plugins", file=sys.stderr)
        return 1

    pending = list(roots) + plugins
    collected: dict[str, str] = {os.path.basename(root): root for root in roots}
    while pending:
        current = pending.pop()
        for dep in dependencies(current):
            base = os.path.basename(dep)
            if is_host_lib(base) or base in collected:
                continue
            if not any(dep.startswith(os.path.realpath(d) + os.sep) for d in LIB_DIRS):
                continue
            collected[base] = dep
            pending.append(dep)

    for name, source in sorted(collected.items()):
        shutil.copy2(source, os.path.join(lib_out, name))

    for name in ("libvlc.so", "libvlccore.so"):
        versioned = [n for n in collected if n.startswith(name + ".")]
        if not versioned:
            print(f"missing {name}", file=sys.stderr)
            return 1
        if not os.path.lexists(os.path.join(lib_out, name)):
            os.symlink(sorted(versioned)[-1], os.path.join(lib_out, name))

    for name in sorted(collected):
        link(lib_out, name)

    for name in os.listdir(lib_out):
        path = os.path.join(lib_out, name)
        if not os.path.islink(path):
            set_rpath(path, "$ORIGIN")

    for entry in PLUGIN_ENTRY_POINTS:
        for alias in (entry + ".so", entry + ".so.5", entry + ".so.9"):
            if os.path.isfile(os.path.join(lib_out, alias)):
                os.symlink(os.path.join("..", "lib", alias), os.path.join(bin_out, alias))

    for plugin in plugins:
        relative = os.path.relpath(plugin, PLUGIN_ROOT)
        target = os.path.join(plugin_out, relative)
        os.makedirs(os.path.dirname(target), exist_ok=True)
        shutil.copy2(plugin, target)
        to_lib = os.path.relpath(lib_out, os.path.dirname(target))
        set_rpath(target, f"$ORIGIN:$ORIGIN/{to_lib}")

    share = "/usr/share/vlc"
    if os.path.isdir(share):
        shutil.copytree(share, os.path.join(out, "share"), symlinks=True)

    licenses = "/usr/share/doc/vlc-plugin-base/copyright"
    if os.path.isfile(licenses):
        shutil.copy2(licenses, os.path.join(out, "COPYING.vlc"))

    total = 0
    for directory, _dirs, names in os.walk(out):
        for name in names:
            path = os.path.join(directory, name)
            if not os.path.islink(path):
                total += os.path.getsize(path)
    print(
        f"collected {len(collected)} libraries, {len(plugins)} plugins, "
        f"{total / 1048576:.1f} MiB"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
