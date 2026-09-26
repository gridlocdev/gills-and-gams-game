# Gills & Gams - run `just` to list recipes

default:
    @just --list

# Run the game
run:
    dotnet run

# Build (debug)
build:
    dotnet build

# Live controller diagnostics window
controllers:
    dotnet run -- --controllers

# Re-render the app icon from the in-game fish
icon:
    dotnet run -- --render-icon assets/icon/AppIcon.png

# Package "Gills & Gams.app" for this Mac's architecture into dist/
build-macos:
    scripts/package-mac.sh

# Package a universal (Apple Silicon + Intel) .app into dist/
build-macos-universal:
    scripts/package-mac.sh --arch universal

# Package a self-contained Linux x64 tarball into dist/ (works from macOS too)
build-linux:
    scripts/package-linux.sh

# Package and launch the .app
run-macos: build-macos
    open "dist/Gills & Gams.app"

# Build the .app and move it into ~/Applications (an older copy goes to the Trash)
install-macos: build-macos
    #!/usr/bin/env bash
    set -euo pipefail
    src="dist/Gills & Gams.app"
    dest="$HOME/Applications/Gills & Gams.app"
    mkdir -p "$HOME/Applications"
    if [[ -e "$dest" ]]; then trash "$dest"; fi
    mv "$src" "$dest"
    echo "Installed to $dest"

# Move the installed app from ~/Applications to the Trash and forget its Input Monitoring permission
uninstall-macos:
    #!/usr/bin/env bash
    set -euo pipefail
    dest="$HOME/Applications/Gills & Gams.app"
    if [[ -e "$dest" ]]; then trash "$dest" && echo "Moved $dest to the Trash"; else echo "Not installed"; fi
    tccutil reset ListenEvent dev.gridloc.gillsandgams >/dev/null 2>&1 && echo "Cleared Input Monitoring permission" || true
