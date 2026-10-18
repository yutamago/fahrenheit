# Fahrenheit

![banner](https://raw.githubusercontent.com/fahrenheit-crew/fahrenheit/refs/heads/main/assets/fh_banner.png)

Fahrenheit is a reverse-engineering project and mod framework for the [Final Fantasy X and X-2 HD Remasters](https://store.steampowered.com/app/359870/).

It provides a comprehensive, ever-growing set of tools for interfacing with the games and their data,
allowing you to modify the game at an unparalleled level and develop entirely new features for it.

It is also the leading repository of knowledge about the games, underpinning other state-of-the-art tools.
This know-how is free for you to use, learn from, analyze, and improve.

## Here just to play?
Download **[fhsetup.exe](https://github.com/fahrenheit-crew/fahrenheit/releases/latest/download/fhsetup.exe)** and run it.
It installs Fahrenheit, finds the game in your Steam library, installs anything missing (.NET, Visual C++ Redistributable)
and creates shortcuts that start the game with Fahrenheit. Run it again later to update Fahrenheit.

On Linux and Steam Deck, download **[fhsetup-linux](https://github.com/fahrenheit-crew/fahrenheit/releases/latest/download/fhsetup-linux)** instead.
Browsers do not keep the executable flag, so allow it to run first (right-click > *Properties* > *Permissions* > *Is executable*,
or `chmod +x fhsetup-linux`). It also needs [Protontricks](https://github.com/Matoking/protontricks), which Steam Deck users can get from the Discover store.

The [installation guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Installation-guide) describes the manual steps,
and how to uninstall.

## For developers
Follow the [build, deployment and debugging guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Build-and-deployment-guide) (or `BUILD.md` locally).

## For modders
[![Fahrenheit.Sdk on NuGet](https://img.shields.io/nuget/vpre/Fahrenheit.Sdk?label=Fahrenheit.Sdk)](https://www.nuget.org/packages/Fahrenheit.Sdk)

Mods are .NET projects built on the [`Fahrenheit.Sdk`](https://www.nuget.org/packages/Fahrenheit.Sdk) package from NuGet.
There is nothing to install by hand. Name the SDK and the Fahrenheit version you target in your project file:

```xml
<Project Sdk="Fahrenheit.Sdk/1.0.0-alpha12">
    <!-- ... -->
</Project>
```

To get started:
- Create your mod from the [mod template](https://github.com/fahrenheit-crew/fh-mods-template) (*Use this template* on GitHub).
- Follow the [modding quick-start guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Modding-quick-start-guide) to build and test it.
- See the [SDK page](https://github.com/fahrenheit-crew/fahrenheit/wiki/SDK) for building against a local or pre-release Fahrenheit.

## Need help, or want to contribute?
Feel free to join us in [Cid's Salvage Ship](https://discord.gg/AGx2grw9nD), a Discord server that supports Fahrenheit and related efforts.

## Looking for more?
Also check out:
- [Karifean](https://github.com/Karifean)'s [FFXDataParser](https://github.com/Karifean/FFXDataParser).
- The [Cutscene Remover](https://github.com/erickt420/FFXCutsceneRemover) mod.
- The [Archipelago](https://github.com/FFX-AP/Archipelago-FFX-Client/wiki) mod.

## License
Fahrenheit source code is licensed under the [LGPL 3.0 or later](https://github.com/fahrenheit-crew/fahrenheit/blob/main/COPYING.LESSER) license.

Assets (the contents of the ``assets`` folder) may be used in forks of Fahrenheit, but _not_ for any other purpose!

For third-party code license notices, please see
[THIRD-PARTY-NOTICES](https://github.com/fahrenheit-crew/fahrenheit/blob/main/THIRD-PARTY-NOTICES).
