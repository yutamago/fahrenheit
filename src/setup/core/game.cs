// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     The result of inspecting a candidate game directory.
/// </summary>
/// <param name="ffx_exe">The full path of <c>FFX.exe</c>, or <see langword="null"/> if it is absent.</param>
/// <param name="ffx2_exe">The full path of <c>FFX-2.exe</c>, or <see langword="null"/> if it is absent.</param>
public sealed record FhGameDirInfo(string path, string? ffx_exe, string? ffx2_exe) {
    public bool is_valid {
        get { return ffx_exe != null && ffx2_exe != null; }
    }
}

public static class FhGameDir {
    public const string STEAM_APP_ID        = "359870";
    public const string STEAM_INSTALL_DIR   = "FINAL FANTASY FFX&FFX-2 HD Remaster";
    public const string FFX_EXE_NAME        = "FFX.exe";
    public const string FFX2_EXE_NAME       = "FFX-2.exe";

    /// <summary>
    ///     Checks whether <paramref name="path"/> is the HD Remaster's game directory, i.e. contains both game executables.
    /// </summary>
    public static FhGameDirInfo inspect(string path) {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) {
            return new FhGameDirInfo(path, null, null);
        }

        return new FhGameDirInfo(
            Path.GetFullPath(path),
            find_file(path, FFX_EXE_NAME),
            find_file(path, FFX2_EXE_NAME));
    }

    /// <summary>
    ///     Finds a file in a directory, ignoring case. Linux file systems are case-sensitive, but Windows games
    ///     are not, so the files may be named differently than expected.
    /// </summary>
    public static string? find_file(string dir, string file_name) {
        string exact = Path.Join(dir, file_name);
        if (File.Exists(exact)) return Path.GetFullPath(exact);

        try {
            foreach (string candidate in Directory.EnumerateFiles(dir)) {
                if (Path.GetFileName(candidate).Equals(file_name, StringComparison.OrdinalIgnoreCase)) {
                    return Path.GetFullPath(candidate);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        return null;
    }
}
