# Fahrenheit SDK

A MSBuild SDK for [Fahrenheit](https://github.com/fahrenheit-crew/fahrenheit), the mod framework for the Final Fantasy X/X-2 HD Remasters.

It provides the necessary build properties and targets to create a compliant mod for a given version of Fahrenheit.

## Usage

Reference the SDK, with the Fahrenheit version your mod targets, at the top of your project file:

```xml
<Project Sdk="Fahrenheit.Sdk/1.0.0-alpha12">
    <!-- ... -->
</Project>
```

A mod built against a given SDK version is only compatible with the matching Fahrenheit release.

## Getting started

- Create a mod from the [mod template](https://github.com/fahrenheit-crew/fh-mods-template).
- Follow the [modding quick-start guide](https://github.com/fahrenheit-crew/fahrenheit/wiki/Modding-quick-start-guide).
- Read the [SDK page](https://github.com/fahrenheit-crew/fahrenheit/wiki/SDK) to build against a local Fahrenheit build.
- Ask for help in [Cid's Salvage Ship](https://discord.gg/AGx2grw9nD) on Discord.
