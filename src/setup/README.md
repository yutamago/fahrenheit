# Fahrenheit Setup

`fhsetup` gets a freshly extracted Fahrenheit release ready to play. It:
- finds the game in the user's Steam libraries, or lets them pick its folder;
- checks for Fahrenheit's prerequisites (the .NET runtime and the Visual C++ Redistributable, x86 and x64),
  and downloads and runs the official Microsoft installers for any that are missing;
- creates the `mods/loadorder` file if it does not exist;
- creates shortcuts that start Final Fantasy X, X-2 and X-2 Last Mission through the Stage 0 loader.

It runs from the `bin` folder of an installation and treats the parent folder as the installation directory.

## Platforms

On **Windows**, prerequisites are installed system-wide and shortcuts are `.lnk` files.
Downloaded installers must carry a valid Authenticode signature from Microsoft.

On **Linux** (including Steam Deck), the game and Fahrenheit's loaders run under Proton. The prerequisites
are installed into the game's Proton prefix, and the shortcuts are desktop entries. Both go through
[Protontricks](https://github.com/Matoking/protontricks) (native or Flatpak), which must be installed.
The game must have been started once through Steam, so that its prefix exists.

## Building

The tool installs the .NET runtime, so it must not need one: release builds use NativeAOT.
- Windows: a Visual Studio MSBuild build of the solution produces a NativeAOT `fhsetup.exe`.
  `dotnet build` produces a framework-dependent tool instead, which needs no C++ toolchain.
- Linux: `dotnet build -c Release -r linux-x64 -p:PublishAot=true src/setup/Fahrenheit.Tools.Setup.csproj` (needs clang).
  CI builds this, and `scripts/ci-release.ps1` adds it to releases.

## Command line

Without arguments, the setup window opens. `fhsetup --help` lists options for scripted setups, such as
`fhsetup --check` or `fhsetup --install-prereqs --create-shortcuts install,desktop`.
