// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     On Linux, the game and Fahrenheit's loaders run under Proton, inside the game's Proton prefix.
///     The prerequisites are the Windows ones, installed into that prefix. Programs are started in the
///     prefix through Protontricks, which knows which Proton version and runtime the game uses.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class FhLinuxPlatform : IFhSetupPlatform {
    private const string _protontricks_flatpak_id = "com.github.Matoking.protontricks";

    public string name {
        get { return "Linux (Proton)"; }
    }

    private static string _home {
        get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
    }

    public List<string> get_steam_roots() {
        return [
            Path.Join(_home, ".steam", "steam"),
            Path.Join(_home, ".local", "share", "Steam"),
            Path.Join(_home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"), // Flatpak
            Path.Join(_home, ".var", "app", "com.valvesoftware.Steam", "data", "Steam"),           // Flatpak, older
            Path.Join(_home, "snap", "steam", "common", ".local", "share", "Steam"),               // Snap
        ];
    }

    public FhPrereqStatus[] check_prereqs(string game_dir) {
        string prefix = FhSteam.get_proton_prefix(game_dir);

        if (!Directory.Exists(prefix)) {
            return Array.ConvertAll(FhPrereqs.all, info => new FhPrereqStatus(info.kind, FhPrereqState.UNKNOWN, null));
        }

        string drive_c    = Path.Join(prefix, "drive_c");
        string system_reg = "";
        try {
            system_reg = File.ReadAllText(Path.Join(prefix, "system.reg"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        return [
            FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X86, Path.Join(drive_c, "Program Files (x86)", "dotnet")),
            FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X64, Path.Join(drive_c, "Program Files",       "dotnet")),
            FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X86, FhPrereqs.read_wine_vcredist(system_reg, FhPrereqKind.VCREDIST_X86)),
            FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X64, FhPrereqs.read_wine_vcredist(system_reg, FhPrereqKind.VCREDIST_X64)),
        ];
    }

    public string? get_install_blocker(string game_dir) {
        if (!Directory.Exists(FhSteam.get_proton_prefix(game_dir))) {
            return "The game's Proton prefix does not exist yet. Start the game once from Steam, quit it, then press 'Check again'.";
        }

        if (_get_protontricks_launch() == null) {
            return "Protontricks is required to install prerequisites and start the game under Proton. "
                 + "Install it (on Steam Deck: from the Discover store), then press 'Check again'.";
        }

        return null;
    }

    public string? get_shortcut_blocker() {
        return _get_protontricks_launch() == null
            ? "Protontricks is required for the shortcuts to start the game under Proton."
            : null;
    }

    /// <summary>
    ///     Finds the command that starts <c>protontricks-launch</c>: either a native install or the Flatpak.
    /// </summary>
    private static string[]? _get_protontricks_launch() {
        string? native = _find_in_path("protontricks-launch");
        if (native != null) return [ native ];

        string? flatpak = _find_in_path("flatpak");
        if (flatpak != null && _run_quiet(flatpak, [ "info", _protontricks_flatpak_id ]) == 0) {
            return [ flatpak, "run", "--command=protontricks-launch", _protontricks_flatpak_id ];
        }

        return null;
    }

    /// <summary>
    ///     Builds a full <c>protontricks-launch</c> command that runs <paramref name="exe"/> in the game's prefix.
    /// </summary>
    private static List<string> _make_launch_command(string[] protontricks_launch, string exe, IEnumerable<string> args) {
        // --no-term: when started from a desktop entry, report errors in a dialog instead of a terminal that does not exist.
        return [ .. protontricks_launch, "--no-term", "--appid", FhGameDir.STEAM_APP_ID, exe, .. args ];
    }

    public void verify_installer(string installer_path) {
        // Authenticode verification needs WinVerifyTrust, which is Windows-only. The download itself is
        // restricted to HTTPS and Microsoft hosts (see FhPrereqs.trusted_download_hosts).
    }

    public async Task<string?> run_installer(FhPrereqInfo info, string installer_path, string game_dir, CancellationToken token) {
        string[]? protontricks_launch = _get_protontricks_launch();
        if (protontricks_launch == null) return "Protontricks was not found.";

        List<string>     command = _make_launch_command(protontricks_launch, installer_path, info.installer_args.Split(' '));
        ProcessStartInfo psi     = new(command[0]) { UseShellExecute = false };
        foreach (string arg in command.Skip(1)) psi.ArgumentList.Add(arg);

        using Process? process = Process.Start(psi);
        if (process == null) return "Protontricks could not be started.";

        await process.WaitForExitAsync(token);
        return process.ExitCode == 0 ? null : $"The installer failed with exit code {process.ExitCode}.";
    }

    public List<FhShortcutLocation> get_shortcut_locations(string install_dir) {
        string data_home = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg_data
            ? xdg_data
            : Path.Join(_home, ".local", "share");

        return [
            new("install", "Fahrenheit folder",  install_dir,                                     enabled_by_default: true),
            new("desktop", "Desktop",            _get_desktop_dir(),                              enabled_by_default: true),
            // Steam's "Add a Non-Steam Game" lists these, which is the easiest way to get Fahrenheit into Game Mode.
            new("menu",    "Applications menu",  Path.Join(data_home, "applications", "fahrenheit"), enabled_by_default: true),
        ];
    }

    /// <summary>
    ///     Reads the desktop directory from <c>user-dirs.dirs</c>, as <c>xdg-user-dir DESKTOP</c> would.
    /// </summary>
    private static string _get_desktop_dir() {
        string config_home = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg_config
            ? xdg_config
            : Path.Join(_home, ".config");

        try {
            foreach (string line in File.ReadLines(Path.Join(config_home, "user-dirs.dirs"))) {
                if (!line.StartsWith("XDG_DESKTOP_DIR=", StringComparison.Ordinal)) continue;

                string value = line["XDG_DESKTOP_DIR=".Length..].Trim('"');
                return value.Replace("$HOME", _home);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }

        return Path.Join(_home, "Desktop");
    }

    public string create_shortcut(FhShortcutLocation location, FhLaunchEntry entry, string bin_dir, string game_dir) {
        string[] protontricks_launch = _get_protontricks_launch()
            ?? throw new InvalidOperationException("Protontricks was not found.");

        string game_exe = FhGameDir.find_file(game_dir, entry.exe_name) ?? Path.Join(game_dir, entry.exe_name);

        List<string> command = _make_launch_command(
            protontricks_launch,
            Path.Join(bin_dir, FhLaunch.STAGE0_EXE_NAME),
            FhLaunch.get_stage0_args(entry, FhLaunch.to_wine_path(game_exe)));

        string desktop_entry = FhLaunch.make_desktop_entry(
            entry.shortcut_name,
            command,
            // Stage 0 locates the rest of Fahrenheit through its working directory.
            bin_dir,
            $"Start {entry.display_name} with Fahrenheit");

        string path = Path.Join(location.dir, $"fahrenheit-{entry.id}.desktop");

        Directory.CreateDirectory(location.dir);
        File.WriteAllText(path, desktop_entry);

        // Desktop environments only run desktop entries on the desktop if they are executable.
        File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);

        return path;
    }

    private static string? _find_in_path(string name) {
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':', StringSplitOptions.RemoveEmptyEntries)) {
            string candidate = Path.Join(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static int _run_quiet(string exe, string[] args) {
        ProcessStartInfo psi = new(exe) {
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
        };
        foreach (string arg in args) psi.ArgumentList.Add(arg);

        try {
            using Process? process = Process.Start(psi);
            if (process == null) return -1;

            process.StandardOutput.ReadToEnd();
            process.StandardError .ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception) {
            return -1;
        }
    }
}
