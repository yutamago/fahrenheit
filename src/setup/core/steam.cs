// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     A copy of the game found in a Steam library.
/// </summary>
/// <param name="library_path">The Steam library folder the game is installed in.</param>
/// <param name="game_dir">The game directory.</param>
public sealed record FhSteamGame(string library_path, string game_dir) {
    /// <summary>
    ///     The game's Proton prefix. It only exists on Linux, once the game has been started through Proton.
    /// </summary>
    public string proton_prefix {
        get { return FhSteam.get_proton_prefix(game_dir); }
    }
}

/// <summary>
///     Locates the game through Steam's library configuration.
/// </summary>
public static class FhSteam {

    /// <summary>
    ///     Gets every library folder known to the Steam installation at <paramref name="steam_root"/>.
    ///     The Steam installation directory is itself always a library.
    /// </summary>
    public static List<string> get_library_paths(string steam_root) {
        List<string> libraries = [ steam_root ];

        string[] candidates = [
            Path.Join(steam_root, "steamapps", "libraryfolders.vdf"),
            Path.Join(steam_root, "config",    "libraryfolders.vdf"),
        ];

        foreach (string vdf_path in candidates) {
            if (!File.Exists(vdf_path)) continue;

            FhVdfNode? folders;
            try {
                folders = FhVdf.parse(File.ReadAllText(vdf_path)).get("libraryfolders");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException) {
                continue;
            }

            if (folders == null) continue;

            foreach ((string key, FhVdfNode node) in folders.children) {
                if (!int.TryParse(key, out _)) continue; // skips "contentstatsid" and the like

                // Current format: "0" { "path" "..." }. Old format: "1" "D:\\SteamLibrary".
                string? path = node.value ?? node.get_value("path");
                if (!string.IsNullOrWhiteSpace(path)) libraries.Add(path);
            }
        }

        return _distinct_paths(libraries);
    }

    /// <summary>
    ///     Finds every copy of the game in the libraries of the given Steam installations.
    /// </summary>
    public static List<FhSteamGame> find_game(IEnumerable<string> steam_roots) {
        List<FhSteamGame> games = [];
        HashSet<string>   seen  = new(_path_comparer);

        foreach (string root in steam_roots) {
            if (!Directory.Exists(root)) continue;

            foreach (string library in get_library_paths(root)) {
                string? game_dir = find_game_in_library(library);
                if (game_dir == null || !seen.Add(_normalize(game_dir))) continue;

                games.Add(new FhSteamGame(library, game_dir));
            }
        }

        return games;
    }

    /// <summary>
    ///     Finds the game in a single Steam library, preferring the install directory named by its app manifest.
    /// </summary>
    public static string? find_game_in_library(string library) {
        string steamapps = Path.Join(library, "steamapps");
        string manifest  = Path.Join(steamapps, $"appmanifest_{FhGameDir.STEAM_APP_ID}.acf");

        List<string> install_dirs = [];

        if (File.Exists(manifest)) {
            try {
                string? install_dir = FhVdf.parse(File.ReadAllText(manifest)).get("AppState")?.get_value("installdir");
                if (!string.IsNullOrWhiteSpace(install_dir)) install_dirs.Add(install_dir);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException) { }
        }

        install_dirs.Add(FhGameDir.STEAM_INSTALL_DIR);

        foreach (string install_dir in install_dirs) {
            string candidate = Path.Join(steamapps, "common", install_dir);
            if (FhGameDir.inspect(candidate).is_valid) return Path.GetFullPath(candidate);
        }

        return null;
    }

    /// <summary>
    ///     Gets the Proton prefix belonging to a game directory in a Steam library
    ///     (<c>{library}/steamapps/common/{game}</c> to <c>{library}/steamapps/compatdata/{app id}/pfx</c>).
    /// </summary>
    public static string get_proton_prefix(string game_dir) {
        string steamapps = Path.GetFullPath(Path.Join(Path.TrimEndingDirectorySeparator(game_dir), "..", ".."));
        return Path.Join(steamapps, "compatdata", FhGameDir.STEAM_APP_ID, "pfx");
    }

    private static readonly StringComparer _path_comparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static string _normalize(string path) {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar)));
    }

    private static List<string> _distinct_paths(List<string> paths) {
        List<string>    result = [];
        HashSet<string> seen   = new(_path_comparer);

        foreach (string path in paths) {
            if (seen.Add(_normalize(path))) result.Add(path);
        }

        return result;
    }
}
