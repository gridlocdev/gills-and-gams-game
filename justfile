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
app:
    scripts/package-mac.sh

# Package a universal (Apple Silicon + Intel) .app into dist/
app-universal:
    scripts/package-mac.sh --arch universal

# Package and launch the .app
app-run: app
    open "dist/Gills & Gams.app"
