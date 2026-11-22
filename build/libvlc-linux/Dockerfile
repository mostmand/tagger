ARG BASE_IMAGE=public.ecr.aws/docker/library/ubuntu:22.04
FROM ${BASE_IMAGE}

# Ubuntu 22.04 is the oldest still-supported desktop baseline (glibc 2.35) and
# gives a VLC 3.0 runtime that LibVLCSharp 3.x expects. The runtime only uses the
# host C library; everything else is bundled.
ARG VLC_VERSION=3.0.16-1build7
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        binutils \
        ca-certificates \
        libvlc5=${VLC_VERSION} \
        patchelf \
        python3 \
        vlc-plugin-base=${VLC_VERSION} \
    && rm -rf /var/lib/apt/lists/*

COPY collect.py /collect.py

RUN python3 /collect.py /out/vlc
