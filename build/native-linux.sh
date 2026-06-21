#!/usr/bin/env bash
set -euo pipefail

# ===================================================================================
# build/native-linux.sh
#
# PURPOSE:
#   Compiles the native (C/C++) Cyclone DDS submodule for Linux and copies the
#   resulting binaries to the local 'artifacts' directory.
#   This is a prerequisite for packing the NuGet package with linux-x64 support.
#
# USAGE:
#   ./build/native-linux.sh [Release|Debug]
#   Default: Release
# ===================================================================================

CONFIG="${1:-Release}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
SOURCE_DIR="$REPO_ROOT/cyclonedds"
BUILD_DIR="$REPO_ROOT/build/native-linux"
INSTALL_DIR="$REPO_ROOT/artifacts/native-install"
ARTIFACTS_DIR="$REPO_ROOT/artifacts/native/linux-x64"

echo "============================================================"
echo "  Building Native CycloneDDS for Linux ($CONFIG)"
echo "============================================================"

# Check prerequisites
if ! command -v cmake &> /dev/null; then
    echo "ERROR: cmake is not installed or not in PATH." >&2
    exit 1
fi

if ! command -v gcc &> /dev/null; then
    echo "ERROR: gcc is not installed or not in PATH." >&2
    exit 1
fi

if [ ! -d "$SOURCE_DIR" ]; then
    echo "ERROR: Native source directory not found: $SOURCE_DIR" >&2
    echo "       Run: git submodule update --init --recursive" >&2
    exit 1
fi

# Ensure output directories exist
mkdir -p "$BUILD_DIR" "$INSTALL_DIR" "$ARTIFACTS_DIR"

# ----------------------------------------------------------------
# [1/3] CMake Configure
# ----------------------------------------------------------------
echo ""
echo "[1/3] Configuring CMake..."

cmake -S "$SOURCE_DIR" -B "$BUILD_DIR" \
    -DCMAKE_INSTALL_PREFIX="$INSTALL_DIR" \
    -DBUILD_IDLC=ON \
    -DBUILD_TESTING=OFF \
    -DBUILD_EXAMPLES=OFF \
    -DENABLE_SSL=OFF \
    -DENABLE_SHM=OFF \
    -DENABLE_SECURITY=OFF \
    -DCMAKE_BUILD_TYPE="$CONFIG"

# ----------------------------------------------------------------
# [2/3] Build & Install
# ----------------------------------------------------------------
echo ""
echo "[2/3] Building & Installing..."

NPROC="${NPROC:-$(nproc 2>/dev/null || echo 4)}"
cmake --build "$BUILD_DIR" --config "$CONFIG" -j "$NPROC"
cmake --install "$BUILD_DIR" --config "$CONFIG"

# ----------------------------------------------------------------
# [3/3] Copy Artifacts & Fix RPATH
# ----------------------------------------------------------------
echo ""
echo "[3/3] Copying artifacts to $ARTIFACTS_DIR..."

# Runtime library (include both .so.0 for soname and .so for convention)
cp -f "$INSTALL_DIR/lib/libddsc.so.0.11.0" "$ARTIFACTS_DIR/libddsc.so.0" 2>/dev/null || echo "  [-] Missing libddsc.so.0.11.0"
cp -f "$INSTALL_DIR/lib/libddsc.so.0.11.0" "$ARTIFACTS_DIR/libddsc.so" 2>/dev/null || true
echo "  [+] libddsc.so / libddsc.so.0"

# IDL compiler executable
cp -f "$INSTALL_DIR/bin/idlc" "$ARTIFACTS_DIR/" 2>/dev/null || echo "  [-] Missing idlc"
echo "  [+] idlc"

# IDL compiler support libraries
for lib in libcycloneddsidl libcycloneddsidlc libcycloneddsidljson; do
    cp -f "$INSTALL_DIR/lib/${lib}.so.0.11.0" "$ARTIFACTS_DIR/${lib}.so.0" 2>/dev/null || echo "  [-] Missing ${lib}.so.0.11.0"
    cp -f "$INSTALL_DIR/lib/${lib}.so.0.11.0" "$ARTIFACTS_DIR/${lib}.so" 2>/dev/null || true
    echo "  [+] ${lib}.so / ${lib}.so.0"
done

# Fix RPATH: cmake sets RPATH to $ORIGIN/../lib (bin/ -> lib/), but in the
# NuGet tools/ directory all files are flat. Change RPATH to $ORIGIN/ so the
# dynamic linker finds .so dependencies alongside the executable.
if command -v patchelf &> /dev/null; then
    echo ""
    echo "  [+] Fixing RPATH to \$ORIGIN/..."
    chmod +w "$ARTIFACTS_DIR/"*.so* "$ARTIFACTS_DIR/idlc" 2>/dev/null || true
    for f in "$ARTIFACTS_DIR/"*.so* "$ARTIFACTS_DIR/idlc"; do
        patchelf --set-rpath '$ORIGIN/' "$f" 2>/dev/null || true
    done
    echo "  [+] RPATH fixed."
else
    echo "  [!] patchelf not found. Run: sudo apt-get install patchelf"
    echo "  [!] Without patchelf, LD_LIBRARY_PATH must be set at runtime."
fi

echo ""
echo "Native build complete."
echo "Artifacts staged at: $ARTIFACTS_DIR"
ls -la "$ARTIFACTS_DIR"
