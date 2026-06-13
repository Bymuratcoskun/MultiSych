#!/bin/bash

# MultiSych Package Builder for Manjaro/Arch Linux
# This script creates a local package that can be installed with pacman

set -e

echo "=== MultiSych Package Builder ==="
echo "Building package for Manjaro KDE..."

# Check if we're in the right directory
if [ ! -f "PKGBUILD" ]; then
    echo "Error: PKGBUILD not found. Run this script from the MultiSych directory."
    exit 1
fi

# Check for required tools
command -v makepkg >/dev/null 2>&1 || { echo "Error: makepkg is required but not installed."; exit 1; }
command -v fakeroot >/dev/null 2>&1 || { echo "Error: fakeroot is required but not installed. Install with 'sudo pacman -S fakeroot' or 'sudo pacman -S base-devel'."; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo "Error: dotnet is required but not installed."; exit 1; }

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf pkg/ src/ *.pkg.tar.zst multisych-1.0.0.tar.gz

# Pack current source tree for makepkg
echo "Creating source tarball..."
TMP_SRC_DIR=$(mktemp -d)
mkdir -p "$TMP_SRC_DIR/multisych-1.0.0"
rsync -a --exclude='pkg' --exclude='src' --exclude='*.pkg.tar.zst' --exclude='multisych-1.0.0.tar.gz' --exclude='.git' ./ "$TMP_SRC_DIR/multisych-1.0.0/"
cd "$TMP_SRC_DIR"
tar -czf "$OLDPWD/multisych-1.0.0.tar.gz" multisych-1.0.0
cd "$OLDPWD"
rm -rf "$TMP_SRC_DIR"

# Build the package
echo "Building package..."
makepkg -f

# Check if package was created
if ls *.pkg.tar.zst 1> /dev/null 2>&1; then
    PACKAGE_FILE=$(ls *.pkg.tar.zst | head -1)
    echo ""
    echo "=== Package Created Successfully ==="
    echo "Package file: $PACKAGE_FILE"
    echo ""
    echo "To install locally:"
    echo "  sudo pacman -U $PACKAGE_FILE"
    echo ""
    echo "To install with dependencies:"
    echo "  sudo pacman -U --asdeps $PACKAGE_FILE"
    echo ""
    echo "After installation, run with:"
    echo "  multisych"
    echo ""
    echo "Or find it in your application menu as 'MultiSych'"
else
    echo "Error: Package creation failed!"
    exit 1
fi
