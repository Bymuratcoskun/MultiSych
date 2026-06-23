#!/bin/bash

# MultiSych Debian Package Builder
# This script creates a .deb package for Debian/Ubuntu based systems.

set -e

echo "=== MultiSych Debian Package Builder ==."
echo "Building package for Debian-based systems..."

# Check if we're in the right directory
if [ ! -d "MultiSych.Desktop" ]; then
    echo "Error: Run this script from the MultiSych project root directory."
    exit 1
fi

# Clean previous build artifacts
echo "Cleaning previous builds..."
rm -rf deb-pkg/ *.deb

# Build and Publish the application
echo "Publishing application in Release configuration..."
dotnet publish MultiSych.Desktop/MultiSych.Desktop.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output "deb-pkg-temp/usr/lib/multisych" \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

# Create control file directory
mkdir -p deb-pkg-temp/DEBIAN

# Create DEBIAN control file
echo "Generating DEBIAN control file..."
cat <<EOF > deb-pkg-temp/DEBIAN/control
Package: multisych
Version: 1.0.0
Section: utils
Priority: optional
Architecture: amd64
Maintainer: MultiSych Team <support@multisych.com>
Depends: sqlite3, libgtk-3-0, libxss1, libnss3, libatk1.0-0, libcairo2, libgdk-pixbuf2.0-0, libglib2.0-0, libpango-1.0-0
Description: Multi-Account Cloud Synchronization Platform with AI support
EOF

# Create other packaging directories
mkdir -p deb-pkg-temp/usr/bin
mkdir -p deb-pkg-temp/usr/share/applications
mkdir -p deb-pkg-temp/usr/share/icons/hicolor/256x256/apps

# Install executable symlink (pointing to absolute installation directory)
ln -sf /usr/lib/multisych/MultiSych.Desktop deb-pkg-temp/usr/bin/multisych

# Copy desktop entry and icon
if [ -f "multisych.desktop" ]; then
    cp multisych.desktop deb-pkg-temp/usr/share/applications/
else
    # Create default desktop entry if not found
    cat <<EOF > deb-pkg-temp/usr/share/applications/multisych.desktop
[Desktop Entry]
Name=MultiSych
Comment=Multi-Account Cloud Synchronization Platform
Exec=multisych
Icon=multisych
Terminal=false
Type=Application
Categories=Utility;FileTransfer;
EOF
fi

if [ -f "multisych.png" ]; then
    cp multisych.png deb-pkg-temp/usr/share/icons/hicolor/256x256/apps/
fi

# Set permissions
chmod -R 755 deb-pkg-temp/usr
chmod 755 deb-pkg-temp/DEBIAN
chmod 644 deb-pkg-temp/DEBIAN/control

# Check if dpkg-deb is available to compile the package
if command -v dpkg-deb >/dev/null 2>&1; then
    echo "Building Debian package (.deb)..."
    dpkg-deb --build deb-pkg-temp multisych_1.0.0_amd64.deb
    rm -rf deb-pkg-temp
    echo ""
    echo "=== Package Created Successfully ==="
    echo "Package file: multisych_1.0.0_amd64.deb"
    echo ""
    echo "To install:"
    echo "  sudo dpkg -i multisych_1.0.0_amd64.deb"
    echo "  sudo apt-get install -f # (to resolve dependencies)"
    echo ""
else
    echo "Warning: 'dpkg-deb' is not available. The directory structure is preserved in 'deb-pkg-temp/'."
    echo "You can build the package manually on a Debian-compatible system with:"
    echo "  dpkg-deb --build deb-pkg-temp multisych_1.0.0_amd64.deb"
fi
