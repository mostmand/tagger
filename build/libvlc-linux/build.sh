#!/usr/bin/env bash
# Build the self-contained Linux libvlc runtime used by the desktop app.
#
# Requires Docker (or Podman with a docker-compatible CLI) and network access on
# the first build. The output is not checked in: release builds run this script
# and ship the result, and local builds can run it once to enable playback.
#
# Usage: build/libvlc-linux/build.sh [output-dir]
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
image="${TAGGER_VLC_IMAGE:-tagger-vlc-linux:3.0.16}"
base_image="${TAGGER_VLC_BASE_IMAGE:-public.ecr.aws/docker/library/ubuntu:22.04}"
output="${1:-${repo_root}/src/Tagger.Desktop/libvlc/linux-x64}"
archive="${TAGGER_VLC_ARCHIVE:-${repo_root}/artifacts/libvlc-linux-x64-3.0.16.tar.gz}"

echo "Building ${image} from ${base_image}…"
docker build \
    --platform linux/amd64 \
    --build-arg "BASE_IMAGE=${base_image}" \
    -t "${image}" \
    "${script_dir}"

container="$(docker create "${image}")"
cleanup() {
    docker rm -f "${container}" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "Extracting runtime to ${output}…"
rm -rf "${output}"
mkdir -p "${output}"
docker cp "${container}:/out/vlc/." "${output}/"

# Write a deterministic archive for release pipelines.
mkdir -p "$(dirname "${archive}")"
tar \
    --sort=name \
    --owner=0 \
    --group=0 \
    --numeric-owner \
    --mtime='2020-01-01 00:00:00Z' \
    -C "${output}" \
    -czf "${archive}" \
    .

size="$(du -sh "${output}" | cut -f1)"
echo "Runtime ready: ${output} (${size})"
echo "Archive:       ${archive}"

if [ -z "${TAGGER_VLC_BASE_IMAGE:-}" ]; then
    echo
    echo "Note: this bundle is built on Ubuntu 22.04 (glibc 2.35+)."
    echo "It contains VLC 3.0.16 under GPL-2.0+; see ${output}/COPYING.vlc."
fi
