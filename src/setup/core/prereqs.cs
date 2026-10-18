// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

public enum FhPrereqKind {
    DOTNET_X86,
    DOTNET_X64,
    VCREDIST_X86,
    VCREDIST_X64,
}

public enum FhPrereqState {
    UNKNOWN,
    INSTALLED,
    MISSING,
    OUTDATED,
}

/// <summary>
///     The result of checking for one prerequisite.
/// </summary>
/// <param name="version">The installed version, or <see langword="null"/> if none was found.</param>
public sealed record FhPrereqStatus(FhPrereqKind kind, FhPrereqState state, Version? version);

/// <summary>
///     Describes a prerequisite and where to get it.
/// </summary>
/// <param name="download_url">An official Microsoft link that always resolves to the latest version of the installer.</param>
/// <param name="installer_args">Arguments that make the installer run unattended, but with a visible progress window.</param>
public sealed record FhPrereqInfo(FhPrereqKind kind, string display_name, string download_url, string installer_args);

/// <summary>
///     Knows which prerequisites Fahrenheit has and how to tell whether they are installed.
/// </summary>
public static class FhPrereqs {

    /// <summary>
    ///     The major version of the .NET runtime Fahrenheit targets (<c>net10.0</c>).
    /// </summary>
    public const int DOTNET_MAJOR = 10;

    /// <summary>
    ///     The oldest Visual C++ Redistributable that can run the Stage 0 and 1 loaders.
    ///     They are built with the Visual Studio 2026 toolset (MSVC 14.50), and a redistributable
    ///     must be at least as new as the toolset that built the binaries using it.
    /// </summary>
    public static readonly Version vcredist_min = new(14, 50);

    public static readonly FhPrereqInfo[] all = [
        new(FhPrereqKind.DOTNET_X86,   $".NET {DOTNET_MAJOR} Runtime (x86)",       $"https://aka.ms/dotnet/{DOTNET_MAJOR}.0/dotnet-runtime-win-x86.exe", "/install /passive /norestart"),
        new(FhPrereqKind.DOTNET_X64,   $".NET {DOTNET_MAJOR} Runtime (x64)",       $"https://aka.ms/dotnet/{DOTNET_MAJOR}.0/dotnet-runtime-win-x64.exe", "/install /passive /norestart"),
        new(FhPrereqKind.VCREDIST_X86, "Visual C++ Redistributable (x86)",          "https://aka.ms/vc14/vc_redist.x86.exe",                             "/install /passive /norestart"),
        new(FhPrereqKind.VCREDIST_X64, "Visual C++ Redistributable (x64)",          "https://aka.ms/vc14/vc_redist.x64.exe",                             "/install /passive /norestart"),
    ];

    /// <summary>
    ///     Hosts the prerequisite downloads may end up on after following redirects. Anything else is rejected.
    /// </summary>
    public static readonly string[] trusted_download_hosts = [
        "aka.ms",
        "builds.dotnet.microsoft.com",
        "dotnetcli.azureedge.net",
        "download.visualstudio.microsoft.com",
        "download.microsoft.com",
    ];

    public static FhPrereqInfo get(FhPrereqKind kind) {
        return Array.Find(all, info => info.kind == kind)!;
    }

    public static bool is_trusted_download(Uri uri) {
        return uri.Scheme == Uri.UriSchemeHttps
            && Array.Exists(trusted_download_hosts, host => uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Checks for a .NET runtime of the targeted major version in a .NET installation directory
    ///     (e.g. <c>C:\Program Files (x86)\dotnet</c>).
    /// </summary>
    public static FhPrereqStatus check_dotnet(FhPrereqKind kind, string? dotnet_root) {
        Version? best = null;

        string shared_fx = dotnet_root == null ? "" : Path.Join(dotnet_root, "shared", "Microsoft.NETCore.App");
        if (Directory.Exists(shared_fx)) {
            foreach (string dir in Directory.EnumerateDirectories(shared_fx)) {
                Version? version = parse_version(Path.GetFileName(dir));
                if (version == null || version.Major != DOTNET_MAJOR) continue;

                // A directory without the runtime library is a leftover from an uninstall.
                if (!File.Exists(Path.Join(dir, "System.Private.CoreLib.dll"))) continue;

                if (best == null || version > best) best = version;
            }
        }

        return new FhPrereqStatus(kind, best == null ? FhPrereqState.MISSING : FhPrereqState.INSTALLED, best);
    }

    /// <summary>
    ///     Classifies an installed Visual C++ Redistributable version.
    /// </summary>
    public static FhPrereqStatus check_vcredist(FhPrereqKind kind, Version? installed) {
        FhPrereqState state = installed == null      ? FhPrereqState.MISSING
                            : installed < vcredist_min ? FhPrereqState.OUTDATED
                            :                            FhPrereqState.INSTALLED;

        return new FhPrereqStatus(kind, state, installed);
    }

    /// <summary>
    ///     Parses a version such as <c>10.0.13</c> or <c>10.0.0-rc.2.25502.107</c>, ignoring any prerelease suffix.
    /// </summary>
    public static Version? parse_version(string text) {
        int suffix = text.IndexOfAny([ '-', '+' ]);
        if (suffix >= 0) text = text[..suffix];

        return Version.TryParse(text, out Version? version) ? version : null;
    }

    /// <summary>
    ///     The registry key, relative to <c>HKEY_LOCAL_MACHINE\SOFTWARE</c>, under which the redistributable registers itself.
    /// </summary>
    public static string get_vcredist_key(FhPrereqKind kind) {
        return kind == FhPrereqKind.VCREDIST_X86
            ? @"Microsoft\VisualStudio\14.0\VC\Runtimes\X86"
            : @"Microsoft\VisualStudio\14.0\VC\Runtimes\X64";
    }

    /// <summary>
    ///     Builds a redistributable version from the <c>Installed</c>, <c>Major</c>, <c>Minor</c> and <c>Bld</c> registry values.
    /// </summary>
    public static Version? make_vcredist_version(uint? installed, uint? major, uint? minor, uint? build) {
        if (installed != 1 || major == null || minor == null) return null;
        return new Version((int)major, (int)minor, (int)(build ?? 0));
    }

    /// <summary>
    ///     Reads the installed redistributable version from the contents of a Wine prefix's <c>system.reg</c>,
    ///     which holds <c>HKEY_LOCAL_MACHINE</c>. Proton prefixes are 64-bit, so the x86 redistributable
    ///     registers itself in the 32-bit view under <c>Wow6432Node</c>.
    /// </summary>
    public static Version? read_wine_vcredist(string system_reg, FhPrereqKind kind) {
        string key = kind == FhPrereqKind.VCREDIST_X86
            ? @"Software\Wow6432Node\" + get_vcredist_key(kind)
            : @"Software\"             + get_vcredist_key(kind);

        Dictionary<string, string>? values = FhWineRegistry.parse(system_reg).GetValueOrDefault(key);
        if (values == null) return null;

        return make_vcredist_version(
            FhWineRegistry.get_dword(values, "Installed"),
            FhWineRegistry.get_dword(values, "Major"),
            FhWineRegistry.get_dword(values, "Minor"),
            FhWineRegistry.get_dword(values, "Bld"));
    }
}

/// <summary>
///     A minimal reader for Wine's text registry files (<c>system.reg</c>, <c>user.reg</c>).
/// </summary>
public static class FhWineRegistry {

    /// <summary>
    ///     Parses a Wine registry file into its keys and their raw (unparsed) values. Key and value names are case-insensitive.
    /// </summary>
    public static Dictionary<string, Dictionary<string, string>> parse(string text) {
        Dictionary<string, Dictionary<string, string>> keys    = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>?                    current = null;

        foreach (string raw_line in text.Split('\n')) {
            string line = raw_line.TrimEnd('\r');

            // A key header looks like `[Software\\Microsoft\\...] 1700000000`. The trailing number is a timestamp.
            if (line.StartsWith('[')) {
                int end = line.IndexOf(']');
                if (end < 0) { current = null; continue; }

                string key = line[1..end].Replace(@"\\", @"\");
                if (!keys.TryGetValue(key, out current)) {
                    current   = new(StringComparer.OrdinalIgnoreCase);
                    keys[key] = current;
                }
                continue;
            }

            // A value looks like `"Name"=dword:0000000e` or `"Name"="string"`. The default value is `@=...`.
            if (current == null || !line.StartsWith('"')) continue;

            int name_end = line.IndexOf("\"=", 1, StringComparison.Ordinal);
            if (name_end < 0) continue;

            current[line[1..name_end]] = line[(name_end + 2)..];
        }

        return keys;
    }

    public static uint? get_dword(Dictionary<string, string> values, string name) {
        if (!values.TryGetValue(name, out string? raw) || !raw.StartsWith("dword:", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        return uint.TryParse(raw.AsSpan(6), System.Globalization.NumberStyles.HexNumber, null, out uint value) ? value : null;
    }
}
