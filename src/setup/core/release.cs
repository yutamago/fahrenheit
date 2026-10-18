// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     A downloadable Fahrenheit release.
/// </summary>
/// <param name="tag">The release's Git tag, e.g. <c>v1.0.0-alpha12</c>.</param>
/// <param name="sha256">The ZIP file's SHA-256 digest as reported by GitHub, or <see langword="null"/> if it reported none.</param>
public sealed record FhRelease(string tag, string zip_name, string zip_url, long zip_size, string? sha256) {
    /// <summary>
    ///     The release's version, comparable with <see cref="FhInstallDirInfo.version"/>.
    /// </summary>
    public string version {
        get { return FhReleases.normalize_version(tag); }
    }
}

/// <summary>
///     Finds Fahrenheit releases on GitHub.
/// </summary>
public static class FhReleases {
    public const string REPOSITORY = "fahrenheit-crew/fahrenheit";

    /// <summary>
    ///     Lists the newest releases. Unlike <c>/releases/latest</c>, this includes pre-releases,
    ///     which is what every Fahrenheit release has been so far.
    /// </summary>
    public static readonly Uri releases_api = new($"https://api.github.com/repos/{REPOSITORY}/releases?per_page=10");

    /// <summary>
    ///     Hosts the release API and downloads may be served from, including after redirects.
    /// </summary>
    public static readonly string[] trusted_hosts = [
        "api.github.com",
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    ];

    public static bool is_trusted(Uri uri) {
        return uri.Scheme == Uri.UriSchemeHttps
            && Array.Exists(trusted_hosts, host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Picks the newest published release that has a release ZIP from the GitHub API's release list,
    ///     which is ordered newest first. Returns <see langword="null"/> if there is none.
    /// </summary>
    public static FhRelease? parse_latest(string releases_json) {
        using JsonDocument doc = JsonDocument.Parse(releases_json);

        foreach (JsonElement release in doc.RootElement.EnumerateArray()) {
            if (release.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean()) continue;

            string? tag = release.GetProperty("tag_name").GetString();
            if (string.IsNullOrEmpty(tag)) continue;

            foreach (JsonElement asset in release.GetProperty("assets").EnumerateArray()) {
                string? name = asset.GetProperty("name").GetString();
                if (name == null || !is_release_zip(name)) continue;

                string? digest = asset.TryGetProperty("digest", out JsonElement d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;

                return new FhRelease(
                    tag,
                    name,
                    asset.GetProperty("browser_download_url").GetString()!,
                    asset.GetProperty("size").GetInt64(),
                    digest != null && digest.StartsWith("sha256:", StringComparison.Ordinal) ? digest["sha256:".Length..] : null);
            }
        }

        return null;
    }

    /// <summary>
    ///     Release ZIPs are named <c>fahrenheit_release_{tag}.zip</c> by <c>scripts/ci-release.ps1</c>.
    /// </summary>
    public static bool is_release_zip(string asset_name) {
        return asset_name.StartsWith("fahrenheit_release_", StringComparison.OrdinalIgnoreCase)
            && asset_name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Reduces a tag (<c>v1.0.0-alpha12</c>) or an informational version (<c>1.0.0-alpha12+ccd3524</c>)
    ///     to a comparable version (<c>1.0.0-alpha12</c>).
    /// </summary>
    public static string normalize_version(string version) {
        int plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];
        return version.TrimStart('v', 'V');
    }
}

public enum FhInstallDirState {
    /// <summary>The folder does not exist or is empty. Fahrenheit can be installed into it.</summary>
    EMPTY,
    /// <summary>The folder holds a Fahrenheit installation. It can be updated.</summary>
    FAHRENHEIT,
    /// <summary>The folder holds other files. Installing would mix them with Fahrenheit's.</summary>
    OTHER,
    /// <summary>The folder is (or is inside) the game folder, where Fahrenheit must not be installed.</summary>
    GAME,
}

/// <summary>
///     The result of inspecting a candidate installation directory.
/// </summary>
/// <param name="version">The installed version, if <see cref="state"/> is <see cref="FhInstallDirState.FAHRENHEIT"/> and it could be read.</param>
public sealed record FhInstallDirInfo(string path, FhInstallDirState state, string? version);

/// <summary>
///     Installs and updates Fahrenheit from a release ZIP.
/// </summary>
public static class FhInstaller {
    public const string BIN_DIR_NAME   = "bin";
    public const string SETUP_DIR_NAME = "setup";

    /// <summary>
    ///     The file whose version identifies an installation's version.
    /// </summary>
    public const string CORE_LIBRARY_NAME = "fh.dll";

    public static FhInstallDirInfo inspect(string path) {
        if (string.IsNullOrWhiteSpace(path)) return new FhInstallDirInfo(path, FhInstallDirState.OTHER, null);

        string full = Path.GetFullPath(path);

        if (FhGameDir.find_file(full, FhGameDir.FFX_EXE_NAME) != null || _is_inside_game_dir(full)) {
            return new FhInstallDirInfo(full, FhInstallDirState.GAME, null);
        }

        if (!Directory.Exists(full) || !Directory.EnumerateFileSystemEntries(full).Any()) {
            return new FhInstallDirInfo(full, FhInstallDirState.EMPTY, null);
        }

        if (File.Exists(Path.Join(full, BIN_DIR_NAME, FhLaunch.STAGE0_EXE_NAME))) {
            return new FhInstallDirInfo(full, FhInstallDirState.FAHRENHEIT, read_version(Path.Join(full, BIN_DIR_NAME, CORE_LIBRARY_NAME)));
        }

        return new FhInstallDirInfo(full, FhInstallDirState.OTHER, null);
    }

    private static bool _is_inside_game_dir(string path) {
        for (DirectoryInfo? dir = new DirectoryInfo(path).Parent; dir != null; dir = dir.Parent) {
            if (FhGameDir.inspect(dir.FullName).is_valid) return true;
        }
        return false;
    }

    /// <summary>
    ///     Reads the product version (e.g. <c>1.0.0-alpha12</c>) of an installed Fahrenheit library.
    /// </summary>
    public static string? read_version(string library_path) {
        if (!File.Exists(library_path)) return null;

        try {
            string? product_version = FileVersionInfo.GetVersionInfo(library_path).ProductVersion;
            return string.IsNullOrWhiteSpace(product_version) ? null : FhReleases.normalize_version(product_version);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or BadImageFormatException) {
            return null;
        }
    }

    /// <summary>
    ///     Installs a release ZIP into <paramref name="install_dir"/>, as the installation guide describes for updates:
    ///     the old <c>bin</c> and <c>setup</c> folders are removed first, so files from different releases never mix.
    ///     Configuration, mods, saves and state are left alone.
    /// </summary>
    /// <param name="running_exe">
    ///     The path of the running setup tool. If the release replaces it, it is renamed out of the way first,
    ///     since a running executable cannot be overwritten.
    /// </param>
    public static void install(string zip_path, string install_dir, string? running_exe) {
        using ZipArchive zip = ZipFile.OpenRead(zip_path);

        if (!zip.Entries.Any(entry => _normalize_entry(entry.FullName).Equals($"{BIN_DIR_NAME}/{FhLaunch.STAGE0_EXE_NAME}", StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidDataException($"The file is not a Fahrenheit release: it contains no {BIN_DIR_NAME}/{FhLaunch.STAGE0_EXE_NAME}.");
        }

        // Check every entry before touching the installation, so a bad file cannot leave it half updated.
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(install_dir)) + Path.DirectorySeparatorChar;
        foreach (ZipArchiveEntry entry in zip.Entries) {
            string target = Path.GetFullPath(Path.Join(root, entry.FullName));
            if (!target.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) {
                throw new InvalidDataException($"The file is not a valid release: '{entry.FullName}' would be extracted outside the installation folder.");
            }
        }

        string? running = running_exe == null ? null : Path.GetFullPath(running_exe);

        Directory.CreateDirectory(install_dir);
        _clear_dir(Path.Join(install_dir, BIN_DIR_NAME),   running);
        _clear_dir(Path.Join(install_dir, SETUP_DIR_NAME), running);

        // Rejects entries that would land outside the installation directory.
        zip.ExtractToDirectory(install_dir, overwriteFiles: true);
    }

    private static string _normalize_entry(string entry_name) {
        return entry_name.Replace('\\', '/').TrimStart('/');
    }

    /// <summary>
    ///     Deletes a folder's contents. The running executable is renamed instead, since it cannot be deleted.
    /// </summary>
    private static void _clear_dir(string dir, string? running_exe) {
        if (!Directory.Exists(dir)) return;

        foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToArray()) {
            if (running_exe != null && Path.GetFullPath(file).Equals(running_exe, StringComparison.OrdinalIgnoreCase)) {
                File.Move(file, file + ".old", overwrite: true);
                continue;
            }
            File.Delete(file);
        }

        foreach (string sub in Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToArray()) {
            if (!Directory.EnumerateFileSystemEntries(sub).Any()) Directory.Delete(sub);
        }
    }

    /// <summary>
    ///     Removes setup tool copies left behind by an earlier update of the running tool.
    /// </summary>
    public static void remove_leftovers(string setup_dir) {
        if (!Directory.Exists(setup_dir)) return;

        foreach (string old in Directory.EnumerateFiles(setup_dir, "*.old")) {
            try {
                File.Delete(old);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    ///     A sensible default installation directory, in the user's home folder where they can find their mods and saves.
    /// </summary>
    public static string get_default_install_dir() {
        return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Fahrenheit");
    }

    /// <summary>
    ///     Finds the installation a setup tool belongs to, if it runs from an installation's <c>setup</c> folder.
    /// </summary>
    public static string? find_install_of_tool(string tool_dir) {
        DirectoryInfo dir    = new(Path.TrimEndingDirectorySeparator(Path.GetFullPath(tool_dir)));
        DirectoryInfo? parent = dir.Parent;

        if (parent == null || !dir.Name.Equals(SETUP_DIR_NAME, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(Path.Join(parent.FullName, BIN_DIR_NAME, FhLaunch.STAGE0_EXE_NAME)) ? parent.FullName : null;
    }
}
