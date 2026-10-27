# Fahrenheit Setup

`fhsetup` installs Fahrenheit and gets it ready to play. Players download it as a single file from the releases page. It:
- installs the newest Fahrenheit release from GitHub into a folder of the player's choice, or updates an existing installation.
- finds the game in the player's Steam libraries, or lets them pick its folder.
- checks for Fahrenheit's prerequisites (the .NET runtime and the Visual C++ Redistributable, x86 and x64),
  and downloads and runs the official Microsoft installers for any that are missing.
- creates the `mods/loadorder` file if it does not exist.
- creates shortcuts that start Final Fantasy X, X-2 and X-2 Last Mission through the Stage 0 loader.

Updates work like the [installation guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Installation-guide#updating)
describes: the `bin` and `setup` folders are replaced, while mods, settings, saves and state are kept.

Releases also contain the tool in their `setup` folder. Run from there, it selects that installation.
It must never be placed in `bin`. Its native libraries (SDL3, cimgui, ...) are 64-bit and share names with the
32-bit ones Fahrenheit loads into the game, and copies in `bin` make Fahrenheit fail to start.

## Platforms

On **Windows**, prerequisites are installed system-wide and shortcuts are `.lnk` files.
Downloaded installers must carry a valid Authenticode signature from Microsoft.

On **Linux** (including Steam Deck), the game and Fahrenheit's loaders run under Proton. The prerequisites
are installed into the game's Proton prefix, and the shortcuts are desktop entries. Both go through
[Protontricks](https://github.com/Matoking/protontricks) (native or Flatpak), which must be installed.
The game must have been started once through Steam, so that its prefix exists.

Release downloads must come from GitHub over HTTPS and match the SHA-256 digest GitHub reports for them.

## Building

The tool installs the .NET runtime, so it must not need one. Release builds therefore use NativeAOT,
and embed the UI's native libraries and font so the tool is a single file (see `natives.cs`).
- Windows: a Visual Studio MSBuild build of the solution produces a NativeAOT `setup/fhsetup.exe`.
  `dotnet build` produces a framework-dependent tool instead, which needs no C++ toolchain.
- Linux: `dotnet build -c Release -r linux-x64 -p:PublishAot=true src/setup/Fahrenheit.Tools.Setup.csproj` (needs clang).
  CI builds this, and `scripts/ci-release.ps1` adds it to the release.

## Command line

Without arguments, the setup window opens. `fhsetup --help` lists options for scripted setups, such as
`fhsetup --check` or `fhsetup --install --create-shortcuts install,desktop`.
To test a build that is not released yet, install its ZIP with `fhsetup --install-zip <path>`.
