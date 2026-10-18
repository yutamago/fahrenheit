// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

internal enum FhLogLevel {
    INFO,
    SUCCESS,
    ERROR,
}

internal sealed record FhLogLine(FhLogLevel level, string text);

/// <summary>
///     How the current game directory was chosen.
/// </summary>
internal enum FhGameDirSource {
    NONE,
    STEAM,
    USER,
}

/// <summary>
///     The setup tool's state and actions, shared by the graphical and command-line interfaces.
/// </summary>
internal sealed class FhSetup {
    public readonly IFhSetupPlatform platform;

    /// <summary>
    ///     The running executable. An update that replaces it renames it out of the way.
    /// </summary>
    public readonly string? tool_path;

    /// <summary>
    ///     The Fahrenheit installation directory. Fahrenheit may not be installed in it yet.
    /// </summary>
    public string           install_dir  { get; private set; } = "";
    public string           bin_dir      { get; private set; } = "";
    public FhInstallDirInfo install_info { get; private set; } = FhInstaller.inspect("");

    /// <summary>
    ///     The newest release on GitHub, once <see cref="check_latest_release"/> has found it.
    /// </summary>
    public FhRelease? latest_release       { get; private set; }
    public string?    latest_release_error { get; private set; }
    public bool       latest_release_known { get; private set; }

    public readonly List<FhSteamGame> detected_games = [];
    public List<FhShortcutLocation>   shortcut_locations { get; private set; } = [];
    public bool[]                     location_enabled   { get; private set; } = [];
    public readonly bool[]            entry_enabled;

    public string          game_dir        { get; private set; } = "";
    public FhGameDirSource game_dir_source { get; private set; }
    public FhGameDirInfo   game_info       { get; private set; } = FhGameDir.inspect("");

    public FhPrereqStatus[] prereqs          { get; private set; } = [];
    public string?          install_blocker  { get; private set; }
    public string?          shortcut_blocker { get; private set; }

    private readonly Lock            _lock = new();
    private readonly List<FhLogLine> _log  = [];

    private volatile bool   _busy;
    private volatile string _busy_status = "";
    private          float  _busy_progress = -1f;

    /// <param name="tool_dir">The folder this tool runs from. If it is an installation's <c>setup</c> folder, that installation is selected.</param>
    public FhSetup(IFhSetupPlatform platform, string tool_dir, string? tool_path) {
        this.platform  = platform;
        this.tool_path = tool_path;
        entry_enabled  = Array.ConvertAll(FhLaunch.entries, _ => true);

        select_install_dir(FhInstaller.find_install_of_tool(tool_dir) ?? FhInstaller.get_default_install_dir());
        FhInstaller.remove_leftovers(Path.Join(install_dir, FhInstaller.SETUP_DIR_NAME));
    }

    /// <summary>
    ///     Whether <see cref="install_dir"/> holds a Fahrenheit installation.
    /// </summary>
    public bool is_installed {
        get { return install_info.state == FhInstallDirState.FAHRENHEIT; }
    }

    /// <summary>
    ///     Whether the installation is older than the latest release, or its version is unknown.
    /// </summary>
    public bool is_update_available {
        get { return is_installed && latest_release != null && install_info.version != latest_release.version; }
    }

    public void select_install_dir(string path) {
        install_info = FhInstaller.inspect(path);
        install_dir  = install_info.path;
        bin_dir      = Path.Join(install_dir, FhInstaller.BIN_DIR_NAME);

        // Keep the user's choice of shortcut locations. Only the installation folder's own location moves.
        Dictionary<string, bool> enabled = new();
        for (int i = 0; i < shortcut_locations.Count; i++) enabled[shortcut_locations[i].id] = location_enabled[i];

        shortcut_locations = platform.get_shortcut_locations(install_dir);
        location_enabled   = shortcut_locations.Select(location => enabled.GetValueOrDefault(location.id, location.enabled_by_default)).ToArray();
    }

    /// <summary>
    ///     Asks GitHub for the newest release.
    /// </summary>
    public async Task check_latest_release(CancellationToken token = default) {
        try {
            string json    = await FhDownloader.get_string(FhReleases.releases_api, FhReleases.is_trusted, token);
            latest_release = FhReleases.parse_latest(json);
            latest_release_error = latest_release == null ? "No Fahrenheit release was found on GitHub." : null;
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            latest_release_error = $"Could not check for the latest release: {e.Message}";
        }
        finally {
            latest_release_known = true;
        }
    }

    /// <summary>
    ///     Explains why Fahrenheit cannot be installed into <see cref="install_dir"/>, or returns <see langword="null"/> if it can.
    /// </summary>
    public string? install_dir_blocker {
        get {
            return install_info.state switch {
                FhInstallDirState.GAME  => "Fahrenheit must be installed outside the game folder.",
                FhInstallDirState.OTHER => "This folder contains other files. Choose an empty folder, or an existing Fahrenheit installation.",
                _                       => null,
            };
        }
    }

    /// <summary>
    ///     Installs the latest release into <see cref="install_dir"/>, or updates the installation there.
    ///     With <paramref name="local_zip"/>, installs that release ZIP instead of downloading one.
    /// </summary>
    public async Task<bool> install_release(string? local_zip = null, CancellationToken token = default) {
        if (_busy) return false;
        _busy = true;

        try {
            if (install_dir_blocker != null) {
                log(FhLogLevel.ERROR, install_dir_blocker);
                return false;
            }

            string zip = local_zip ?? "";
            string label;

            if (local_zip == null) {
                if (latest_release == null) {
                    log(FhLogLevel.ERROR, latest_release_error ?? "The latest release is not known yet.");
                    return false;
                }

                label        = $"Fahrenheit {latest_release.tag}";
                _busy_status = $"Downloading {label}...";
                Volatile.Write(ref _busy_progress, 0f);
                log(FhLogLevel.INFO, _busy_status);

                if (latest_release.sha256 == null) log(FhLogLevel.INFO, "GitHub reported no checksum for this release, so the download cannot be verified.");

                zip = await FhDownloader.download_file(
                    new Uri(latest_release.zip_url), FhReleases.is_trusted, _download_dir,
                    latest_release.zip_name, latest_release.sha256, p => Volatile.Write(ref _busy_progress, p), token);
            }
            else {
                label = Path.GetFileName(local_zip);
            }

            bool updating = is_installed;
            _busy_status  = updating ? $"Updating to {label}..." : $"Installing {label}...";
            Volatile.Write(ref _busy_progress, -1f);
            log(FhLogLevel.INFO, _busy_status);

            await Task.Run(() => FhInstaller.install(zip, install_dir, tool_path), token);
            if (local_zip == null) File.Delete(zip);

            select_install_dir(install_dir);
            log(FhLogLevel.SUCCESS, updating ? $"Updated {install_dir} to {label}." : $"Installed {label} into {install_dir}.");
            return ensure_load_order();
        }
        catch (Exception e) when (e is not OperationCanceledException) {
            log(FhLogLevel.ERROR, $"Could not install Fahrenheit: {e.Message}");
            select_install_dir(install_dir);
            return false;
        }
        finally {
            _busy_status = "";
            _busy        = false;
        }
    }

    public bool is_busy {
        get { return _busy; }
    }

    public string busy_status {
        get { return _busy_status; }
    }

    /// <summary>
    ///     The progress of the current operation (0 to 1), or a negative value if unknown.
    /// </summary>
    public float busy_progress {
        get { return Volatile.Read(ref _busy_progress); }
    }

    public FhLogLine[] get_log() {
        lock (_lock) return [ .. _log ];
    }

    public void log(FhLogLevel level, string text) {
        lock (_lock) _log.Add(new FhLogLine(level, text));
    }

    /// <summary>
    ///     Looks for the game in the Steam libraries and selects the first copy found.
    /// </summary>
    public void detect() {
        detected_games.Clear();
        detected_games.AddRange(FhSteam.find_game(platform.get_steam_roots()));

        if (detected_games.Count > 0) {
            set_game_dir(detected_games[0].game_dir, FhGameDirSource.STEAM);
        }
        else {
            refresh_prereqs();
        }
    }

    public void set_game_dir(string path, FhGameDirSource source) {
        game_dir  = path;
        game_info = FhGameDir.inspect(path);

        // A directory the user picked that matches a detected copy still counts as found in Steam.
        bool is_detected = game_info.is_valid && detected_games.Exists(game => _same_path(game.game_dir, game_info.path));
        game_dir_source  = string.IsNullOrWhiteSpace(path) ? FhGameDirSource.NONE
                         : is_detected                     ? FhGameDirSource.STEAM
                         :                                   source;

        refresh_prereqs();
    }

    public void refresh_prereqs() {
        // Without a game directory, the Proton prefix (and thus the prerequisites) on Linux cannot be located.
        string context = game_info.is_valid ? game_info.path : "";

        prereqs         = context.Length > 0 || OperatingSystem.IsWindows()
            ? platform.check_prereqs(context)
            : Array.ConvertAll(FhPrereqs.all, info => new FhPrereqStatus(info.kind, FhPrereqState.UNKNOWN, null));
        install_blocker = context.Length > 0 || OperatingSystem.IsWindows()
            ? platform.get_install_blocker(context)
            : "Select the game folder first.";

        shortcut_blocker = platform.get_shortcut_blocker();
    }

    public FhPrereqStatus[] missing_prereqs {
        get { return Array.FindAll(prereqs, status => status.state != FhPrereqState.INSTALLED); }
    }

    /// <summary>
    ///     Downloads and runs the installer of every missing or outdated prerequisite, one after another.
    /// </summary>
    public async Task install_missing_prereqs(CancellationToken token = default) {
        if (_busy) return;
        _busy = true;

        try {

            foreach (FhPrereqStatus status in missing_prereqs) {
                FhPrereqInfo info = FhPrereqs.get(status.kind);

                _busy_status = $"Downloading {info.display_name}...";
                Volatile.Write(ref _busy_progress, 0f);
                log(FhLogLevel.INFO, _busy_status);

                string installer;
                try {
                    installer = await FhDownloader.download(info, _download_dir, p => Volatile.Write(ref _busy_progress, p), token);
                    platform.verify_installer(installer);
                }
                catch (Exception e) when (e is not OperationCanceledException) {
                    log(FhLogLevel.ERROR, $"Could not download {info.display_name}: {e.Message}");
                    continue;
                }

                _busy_status = $"Installing {info.display_name}...";
                Volatile.Write(ref _busy_progress, -1f);
                log(FhLogLevel.INFO, _busy_status);

                string? error;
                try {
                    error = await platform.run_installer(info, installer, game_info.path, token);
                }
                catch (Exception e) when (e is not OperationCanceledException) {
                    error = e.Message;
                }

                // Installers can exit successfully without installing anything, so check for the prerequisite again.
                bool detected = Array.Exists(platform.check_prereqs(game_info.path), s => s.kind == info.kind && s.state == FhPrereqState.INSTALLED);

                if      (error != null) log(FhLogLevel.ERROR,   $"{info.display_name}: {error}");
                else if (!detected)     log(FhLogLevel.ERROR,   $"The {info.display_name} installer finished, but it is still not detected.");
                else                    log(FhLogLevel.SUCCESS, $"Installed {info.display_name}.");

                try {
                    File.Delete(installer);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        finally {
            refresh_prereqs();
            _busy_status = "";
            _busy        = false;
        }
    }

    /// <summary>
    ///     Creates the selected shortcuts, and the mod load order file if it does not exist yet.
    ///     Returns whether everything succeeded.
    /// </summary>
    public bool finish() {
        if (!is_installed) {
            log(FhLogLevel.ERROR, "Install Fahrenheit first.");
            return false;
        }

        if (!game_info.is_valid) {
            log(FhLogLevel.ERROR, "Select a valid game folder first.");
            return false;
        }

        if (shortcut_blocker != null) {
            log(FhLogLevel.ERROR, shortcut_blocker);
            return false;
        }

        bool ok = ensure_load_order();

        for (int l = 0; l < shortcut_locations.Count; l++) {
            if (!location_enabled[l]) continue;

            for (int e = 0; e < FhLaunch.entries.Length; e++) {
                if (!entry_enabled[e]) continue;

                try {
                    string path = platform.create_shortcut(shortcut_locations[l], FhLaunch.entries[e], bin_dir, game_info.path);
                    log(FhLogLevel.SUCCESS, $"Created {path}");
                }
                catch (Exception ex) {
                    log(FhLogLevel.ERROR, $"Could not create the {FhLaunch.entries[e].display_name} shortcut in {shortcut_locations[l].display_name}: {ex.Message}");
                    ok = false;
                }
            }
        }

        return ok;
    }

    /// <summary>
    ///     Fahrenheit refuses to start without <c>mods/loadorder</c>. Creates an empty one if it is missing.
    /// </summary>
    public bool ensure_load_order() {
        string path = Path.Join(install_dir, "mods", "loadorder");
        if (File.Exists(path)) return true;

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");
            log(FhLogLevel.SUCCESS, $"Created the empty mod load order file {path}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
            log(FhLogLevel.ERROR, $"Could not create {path}: {e.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Downloads go to a temporary folder, never into the installation folder: a failed download would
    ///     otherwise leave files behind that make an empty folder look like it holds something else.
    /// </summary>
    private static string _download_dir {
        get { return Path.Join(Path.GetTempPath(), "fhsetup-downloads"); }
    }

    private static bool _same_path(string a, string b) {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            comparison);
    }
}
