// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     A place shortcuts can be created in.
/// </summary>
/// <param name="dir">The directory the shortcuts are written to.</param>
internal sealed record FhShortcutLocation(string id, string display_name, string dir, bool enabled_by_default);

/// <summary>
///     Everything the setup tool does that differs between operating systems.
/// </summary>
internal interface IFhSetupPlatform {
    /// <summary>
    ///     A short, user-facing description of how the game is run, e.g. "Windows" or "Linux (Proton)".
    /// </summary>
    string name { get; }

    /// <summary>
    ///     Candidate Steam installation directories. They need not exist.
    /// </summary>
    List<string> get_steam_roots();

    /// <summary>
    ///     Checks every prerequisite where the game will run: on the system, or in the game's Proton prefix.
    /// </summary>
    FhPrereqStatus[] check_prereqs(string game_dir);

    /// <summary>
    ///     Explains why prerequisites cannot be installed automatically right now, or returns <see langword="null"/> if they can.
    /// </summary>
    string? get_install_blocker(string game_dir);

    /// <summary>
    ///     Verifies that a downloaded installer is an untampered Microsoft binary. Throws if it is not.
    /// </summary>
    void verify_installer(string installer_path);

    /// <summary>
    ///     Runs a downloaded installer to completion. Returns a user-facing failure message, or <see langword="null"/> on success.
    /// </summary>
    Task<string?> run_installer(FhPrereqInfo info, string installer_path, string game_dir, CancellationToken token);

    /// <summary>
    ///     Explains why shortcuts cannot be created right now, or returns <see langword="null"/> if they can.
    /// </summary>
    string? get_shortcut_blocker();

    /// <summary>
    ///     The places shortcuts can be created in.
    /// </summary>
    List<FhShortcutLocation> get_shortcut_locations(string install_dir);

    /// <summary>
    ///     Creates a shortcut that starts <paramref name="entry"/> through Stage 0. Returns the path of the created file.
    /// </summary>
    string create_shortcut(FhShortcutLocation location, FhLaunchEntry entry, string bin_dir, string game_dir);
}

internal static class FhSetupPlatform {
    public static IFhSetupPlatform create() {
        if (OperatingSystem.IsWindows()) return new FhWindowsPlatform();
        if (OperatingSystem.IsLinux())   return new FhLinuxPlatform();

        throw new PlatformNotSupportedException("Fahrenheit Setup only runs on Windows and Linux.");
    }
}
