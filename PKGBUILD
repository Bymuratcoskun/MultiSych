# Maintainer: MultiSych Team <support@multisych.com>
pkgname=multisych
pkgver=1.0.0
pkgrel=1
pkgdesc="Multi-Account Cloud Synchronization Platform with AI support"
arch=('x86_64')
url="https://github.com/yourusername/MultiSych"
license=('MIT')
depends=(
    'dotnet-runtime-10.0'
    'gtk3'
    'libxss'
    'nss'
    'atk'
    'at-spi2-core'
    'cairo'
    'gdk-pixbuf2'
    'glib2'
    'glibc'
    'pango'
    'libx11'
    'libxcomposite'
    'libxcursor'
    'libxdamage'
    'libxext'
    'libxfixes'
    'libxi'
    'libxrandr'
    'libxrender'
    'libxtst'
    'sqlite'
    'openssl'
)
makedepends=(
    'dotnet-sdk-10.0'
    'fakeroot'
)
optdepends=(
    'xdg-utils: for desktop integration'
    'xdg-desktop-portal: for file dialogs'
)
source=(
    "multisych-1.0.0.tar.gz"
    "multisych.desktop"
    "multisych.png"
)
sha256sums=(
    'SKIP'
    'SKIP'
    'SKIP'
)

prepare() {
    cd "$srcdir/multisych-1.0.0"

    # Restore NuGet packages
    dotnet restore

    # Build the application
    dotnet build --configuration Release --no-restore
}

build() {
    cd "$srcdir/multisych-1.0.0"

    # Publish the desktop application
    dotnet publish MultiSych.Desktop/MultiSych.Desktop.csproj \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained true \
        --output "$srcdir/publish" \
        /p:PublishTrimmed=false \
        /p:PublishSingleFile=false
}

package() {
    cd "$srcdir"

    # Create directories
    install -dm755 "$pkgdir/usr/lib/multisych"
    install -dm755 "$pkgdir/usr/bin"
    install -dm755 "$pkgdir/usr/share/applications"
    install -dm755 "$pkgdir/usr/share/icons/hicolor/256x256/apps"
    install -dm755 "$pkgdir/usr/share/doc/multisych"

    # Install application files
    cp -r publish/* "$pkgdir/usr/lib/multisych/"

    # Create executable symlink
    ln -s /usr/lib/multisych/MultiSych.Desktop "$pkgdir/usr/bin/multisych"

    # Install desktop file
    install -Dm644 multisych.desktop "$pkgdir/usr/share/applications/multisych.desktop"

    # Install icon
    install -Dm644 multisych.png "$pkgdir/usr/share/icons/hicolor/256x256/apps/multisych.png"

    # Install documentation
    install -Dm644 multisych-1.0.0/README.md "$pkgdir/usr/share/doc/multisych/README.md"
    install -Dm644 multisych-1.0.0/LICENSE "$pkgdir/usr/share/licenses/multisych/LICENSE"

    # Create configuration directory
    install -dm755 "$pkgdir/etc/multisych"

    # Create user data directory
    install -dm755 "$pkgdir/var/lib/multisych"
}
