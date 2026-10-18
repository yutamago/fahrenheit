// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

internal enum FhStatusIcon {
    OK,
    ERROR,
    WARNING,
    PENDING,
}

/// <summary>
///     Which path field a folder dialog fills in.
/// </summary>
internal enum FhPathField {
    INSTALL_DIR,
    GAME_DIR,
}

/// <summary>
///     The setup window: one page with four steps (Fahrenheit, game folder, prerequisites, shortcuts) and a log.
/// </summary>
internal sealed unsafe class FhSetupUi : IDisposable {
    private const int _path_buffer_size = 4096;

    private static readonly Vector4 _color_ok      = new(0.36f, 0.78f, 0.42f, 1f);
    private static readonly Vector4 _color_error   = new(0.90f, 0.33f, 0.30f, 1f);
    private static readonly Vector4 _color_warning = new(0.95f, 0.72f, 0.25f, 1f);
    private static readonly Vector4 _color_pending = new(0.55f, 0.58f, 0.62f, 1f);
    private static readonly Vector4 _color_muted   = new(0.65f, 0.67f, 0.70f, 1f);

    private readonly FhSetup    _setup;
    private readonly SDLWindow* _window;
    private readonly byte*      _install_dir_buffer;
    private readonly byte*      _game_dir_buffer;

    // The folder dialog may report its result from another thread. It is handed over through this field.
    private static string?                        _picked_dir;
    private static FhPathField                    _picked_field;
    private static readonly SDLDialogFileCallback _on_folder_picked = _folder_picked;

    private int _log_count_seen;

    public FhSetupUi(FhSetup setup, SDLWindow* window) {
        _setup              = setup;
        _window             = window;
        _install_dir_buffer = (byte*)NativeMemory.AllocZeroed(_path_buffer_size);
        _game_dir_buffer    = (byte*)NativeMemory.AllocZeroed(_path_buffer_size);
        _write_path_buffer(_install_dir_buffer, setup.install_dir);
        _write_path_buffer(_game_dir_buffer,    setup.game_dir);
    }

    public void Dispose() {
        NativeMemory.Free(_install_dir_buffer);
        NativeMemory.Free(_game_dir_buffer);
    }

    public static void apply_style(ImGuiStylePtr style) {
        ImGui.StyleColorsDark();
        style.WindowPadding    = new Vector2(20, 16);
        style.FramePadding     = new Vector2(10, 6);
        style.ItemSpacing      = new Vector2(10, 8);
        style.FrameRounding    = 4f;
        style.ChildRounding    = 4f;
        style.GrabRounding     = 4f;
        style.WindowBorderSize = 0f;
    }

    public void draw() {
        _consume_picked_dir();

        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos (viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);

        ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings;
        if (!ImGui.Begin("##fhsetup", flags)) {
            ImGui.End();
            return;
        }

        ImGui.PushFont(ImGui.GetFont(), ImGui.GetFontSize() * 1.4f);
        ImGui.Text("Fahrenheit Setup");
        ImGui.PopFont();
        ImGui.TextColored(_color_muted, $"Installs Fahrenheit and prepares it to run on {_setup.platform.name}.");

        ImGui.Spacing();
        _draw_install();
        ImGui.Spacing();
        _draw_game_dir();
        ImGui.Spacing();
        _draw_prereqs();
        ImGui.Spacing();
        _draw_shortcuts();
        ImGui.Spacing();
        _draw_busy();
        _draw_log();

        ImGui.End();
    }

    private void _draw_install() {
        ImGui.SeparatorText("1. Fahrenheit");
        ImGui.TextWrapped("The folder Fahrenheit is installed in. Your mods, settings and saves are kept there too.");

        if (_path_input("##install_dir", _install_dir_buffer, FhPathField.INSTALL_DIR)) {
            _setup.select_install_dir(_read_path_buffer(_install_dir_buffer));
        }

        FhInstallDirInfo info   = _setup.install_info;
        FhRelease?       latest = _setup.latest_release;

        if (_setup.install_dir_blocker != null) {
            _status_line(FhStatusIcon.ERROR, _setup.install_dir_blocker);
        }
        else if (info.state == FhInstallDirState.FAHRENHEIT) {
            string version = info.version ?? "of an unknown version";
            if (_setup.is_update_available) _status_line(FhStatusIcon.WARNING, $"Fahrenheit {version} is installed. Version {latest!.version} is available.");
            else if (latest != null)        _status_line(FhStatusIcon.OK,      $"Fahrenheit {version} is installed and up to date.");
            else                            _status_line(FhStatusIcon.OK,      $"Fahrenheit {version} is installed.");
        }
        else {
            _status_line(FhStatusIcon.PENDING, "Fahrenheit will be installed into this folder.");
        }

        if (!_setup.latest_release_known) {
            _status_line(FhStatusIcon.PENDING, "Checking GitHub for the latest release...");
        }
        else if (_setup.latest_release_error != null) {
            _status_line(FhStatusIcon.ERROR, _setup.latest_release_error);
        }

        bool needs_install = !_setup.is_installed || _setup.is_update_available;
        if (!needs_install || latest == null) return;

        bool can_install = _setup.install_dir_blocker == null && !_setup.is_busy;
        ImGui.BeginDisabled(!can_install);
        if (ImGui.Button(_setup.is_installed ? $"Update to {latest.tag}" : $"Install Fahrenheit {latest.tag}")) {
            _ = Task.Run(() => _setup.install_release());
        }
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && can_install) {
            ImGui.SetTooltip(_setup.is_installed
                ? "Replaces the 'bin' and 'setup' folders. Your mods, settings and saves are kept."
                : "Downloads the release from GitHub and extracts it into this folder.");
        }
    }

    private void _draw_game_dir() {
        ImGui.SeparatorText("2. Game folder");
        ImGui.TextWrapped("The folder of FINAL FANTASY X/X-2 HD Remaster, which contains FFX.exe and FFX-2.exe.");

        if (_path_input("##game_dir", _game_dir_buffer, FhPathField.GAME_DIR)) {
            _setup.set_game_dir(_read_path_buffer(_game_dir_buffer), FhGameDirSource.USER);
        }

        FhGameDirInfo info = _setup.game_info;

        if (_setup.game_dir.Length == 0) {
            _status_line(FhStatusIcon.WARNING, "The game was not found automatically. Choose its folder with 'Browse...'.");
        }
        else if (!info.is_valid) {
            string missing = (info.ffx_exe, info.ffx2_exe) switch {
                (null, null) => "FFX.exe and FFX-2.exe were not found in this folder.",
                (null, _)    => "FFX.exe was not found in this folder.",
                _            => "FFX-2.exe was not found in this folder.",
            };
            _status_line(FhStatusIcon.ERROR, missing);
        }
        else if (_setup.game_dir_source == FhGameDirSource.STEAM) {
            _status_line(FhStatusIcon.OK, "Found automatically in your Steam library.");
        }
        else {
            _status_line(FhStatusIcon.OK, "Game found in this folder.");
        }

        if (_setup.detected_games.Count > 1) {
            ImGui.TextColored(_color_muted, "Other copies found in Steam:");
            foreach (FhSteamGame game in _setup.detected_games) {
                if (game.game_dir == info.path) continue;
                if (ImGui.SmallButton($"Use {game.game_dir}")) _set_game_dir(game.game_dir, FhGameDirSource.STEAM);
            }
        }
    }

    private void _draw_prereqs() {
        ImGui.SeparatorText("3. Prerequisites");

        foreach (FhPrereqStatus status in _setup.prereqs) {
            FhPrereqInfo info = FhPrereqs.get(status.kind);

            (FhStatusIcon icon, string text) = status.state switch {
                FhPrereqState.INSTALLED => (FhStatusIcon.OK,      $"installed ({status.version})"),
                FhPrereqState.OUTDATED  => (FhStatusIcon.WARNING, $"outdated ({status.version}, need {FhPrereqs.vcredist_min} or newer)"),
                FhPrereqState.MISSING   => (FhStatusIcon.ERROR,   "not installed"),
                _                       => (FhStatusIcon.PENDING, "unknown"),
            };

            _icon(icon);
            ImGui.SameLine();
            ImGui.Text(info.display_name);
            ImGui.SameLine(ImGui.GetFontSize() * 18f);
            ImGui.TextColored(_color_muted, text);
        }

        if (_setup.install_blocker != null) {
            _status_line(FhStatusIcon.WARNING, _setup.install_blocker);
        }

        int  missing     = _setup.missing_prereqs.Length;
        bool can_install = missing > 0 && _setup.install_blocker == null && !_setup.is_busy;

        ImGui.BeginDisabled(!can_install);
        if (ImGui.Button(missing > 0 ? $"Install {missing} missing" : "Everything is installed")) {
            _ = Task.Run(() => _setup.install_missing_prereqs());
        }
        ImGui.EndDisabled();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && can_install) {
            ImGui.SetTooltip("Downloads the official installers from Microsoft and runs them.\nWindows will ask for administrator permission.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Check again")) _setup.refresh_prereqs();
    }

    private void _draw_shortcuts() {
        ImGui.SeparatorText("4. Shortcuts");
        ImGui.TextWrapped("Shortcuts start the game with Fahrenheit. Create them in:");

        for (int i = 0; i < _setup.shortcut_locations.Count; i++) {
            FhShortcutLocation location = _setup.shortcut_locations[i];
            if (i > 0) ImGui.SameLine();
            ImGui.Checkbox($"{location.display_name}##loc{i}", ref _setup.location_enabled[i]);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(location.dir);
        }

        ImGui.AlignTextToFramePadding();
        ImGui.Text("For:");
        for (int i = 0; i < FhLaunch.entries.Length; i++) {
            ImGui.SameLine();
            ImGui.Checkbox($"{FhLaunch.entries[i].display_name}##entry{i}", ref _setup.entry_enabled[i]);
        }

        bool can_finish = _setup.is_installed && _setup.game_info.is_valid && _setup.shortcut_blocker == null && !_setup.is_busy;
        ImGui.BeginDisabled(!can_finish);
        if (ImGui.Button("Create shortcuts")) _setup.finish();
        ImGui.EndDisabled();

        if (!_setup.is_installed) {
            ImGui.SameLine();
            ImGui.TextColored(_color_warning, "Install Fahrenheit first.");
        }
        else if (_setup.shortcut_blocker != null) {
            ImGui.SameLine();
            ImGui.TextColored(_color_warning, _setup.shortcut_blocker);
        }
        else if (_setup.missing_prereqs.Length > 0) {
            ImGui.SameLine();
            ImGui.TextColored(_color_warning, "The game will not start with Fahrenheit until all prerequisites are installed.");
        }
    }

    private void _draw_busy() {
        if (!_setup.is_busy) return;

        float progress = _setup.busy_progress;
        ImGui.ProgressBar(progress >= 0 ? progress : -1f * (float)ImGui.GetTime(), new Vector2(-1, 0), _setup.busy_status);
    }

    /// <summary>
    ///     A path text field with a 'Browse...' button. Returns whether the user edited the text.
    /// </summary>
    private bool _path_input(string id, byte* buffer, FhPathField field) {
        float browse_width = ImGui.CalcTextSize("Browse...").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetNextItemWidth(-browse_width - ImGui.GetStyle().ItemSpacing.X);

        ImGui.BeginDisabled(_setup.is_busy);
        bool edited = ImGui.InputText(id, buffer, _path_buffer_size);

        ImGui.SameLine();
        ImGui.PushID(id);
        if (ImGui.Button("Browse...")) _open_folder_dialog(field, _read_path_buffer(buffer));
        ImGui.PopID();
        ImGui.EndDisabled();

        return edited;
    }

    private void _draw_log() {
        FhLogLine[] lines = _setup.get_log();

        ImGui.BeginChild("##log", new Vector2(-1, -1), ImGuiChildFlags.Borders);
        foreach (FhLogLine line in lines) {
            Vector4 color = line.level switch {
                FhLogLevel.SUCCESS => _color_ok,
                FhLogLevel.ERROR   => _color_error,
                _                  => _color_muted,
            };
            ImGui.PushStyleColor(ImGuiCol.Text, color);
            ImGui.TextWrapped(line.text);
            ImGui.PopStyleColor();
        }

        if (lines.Length != _log_count_seen) {
            ImGui.SetScrollHereY(1f);
            _log_count_seen = lines.Length;
        }
        ImGui.EndChild();
    }

    private static void _status_line(FhStatusIcon icon, string text) {
        _icon(icon);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, icon switch {
            FhStatusIcon.OK      => _color_ok,
            FhStatusIcon.ERROR   => _color_error,
            FhStatusIcon.WARNING => _color_warning,
            _                    => _color_muted,
        });
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    /// <summary>
    ///     Draws a round status icon the height of a text line. The default font has no symbol glyphs, so the icons are drawn.
    /// </summary>
    private static void _icon(FhStatusIcon icon) {
        float          size   = ImGui.GetTextLineHeight();
        Vector2        origin = ImGui.GetCursorScreenPos();
        Vector2        center = origin + new Vector2(size / 2f, size / 2f);
        float          radius = size * 0.42f;
        float          stroke = MathF.Max(1.5f, size * 0.11f);
        ImDrawListPtr  dl     = ImGui.GetWindowDrawList();
        uint           white  = ImGui.GetColorU32(new Vector4(1, 1, 1, 1));

        Vector4 color = icon switch {
            FhStatusIcon.OK      => _color_ok,
            FhStatusIcon.ERROR   => _color_error,
            FhStatusIcon.WARNING => _color_warning,
            _                    => _color_pending,
        };
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(color));

        float r = radius * 0.5f;
        switch (icon) {
            case FhStatusIcon.OK:
                dl.AddLine(center + new Vector2(-r, 0),           center + new Vector2(-r * 0.25f, r * 0.7f), white, stroke);
                dl.AddLine(center + new Vector2(-r * 0.25f, r * 0.7f), center + new Vector2(r, -r * 0.6f),    white, stroke);
                break;
            case FhStatusIcon.ERROR:
                dl.AddLine(center + new Vector2(-r, -r), center + new Vector2(r, r),  white, stroke);
                dl.AddLine(center + new Vector2(-r, r),  center + new Vector2(r, -r), white, stroke);
                break;
            case FhStatusIcon.WARNING:
                dl.AddLine(center + new Vector2(0, -r * 1.1f), center + new Vector2(0, r * 0.3f), white, stroke);
                dl.AddCircleFilled(center + new Vector2(0, r * 0.95f), stroke * 0.65f, white);
                break;
            default:
                dl.AddLine(center + new Vector2(-r, 0), center + new Vector2(r, 0), white, stroke);
                break;
        }

        ImGui.Dummy(new Vector2(size, size));
    }

    private void _set_game_dir(string path, FhGameDirSource source) {
        _setup.set_game_dir(path, source);
        _write_path_buffer(_game_dir_buffer, path);
    }

    private void _set_install_dir(string path) {
        _setup.select_install_dir(path);
        _write_path_buffer(_install_dir_buffer, _setup.install_dir);
    }

    private void _open_folder_dialog(FhPathField field, string current) {
        // Open the dialog at the current folder, so the user sees where the detected one is.
        string? start = Directory.Exists(current) ? current : null;
        _picked_field = field;
        SDL.ShowOpenFolderDialog(_on_folder_picked, null, _window, start, false);
    }

    private static void _folder_picked(void* userdata, byte** filelist, int filter) {
        // A null list is an error. An empty list means the user cancelled.
        if (filelist == null || filelist[0] == null) return;
        Volatile.Write(ref _picked_dir, Marshal.PtrToStringUTF8((nint)filelist[0]));
    }

    private void _consume_picked_dir() {
        string? picked = Interlocked.Exchange(ref _picked_dir, null);
        if (picked == null) return;

        if (_picked_field == FhPathField.INSTALL_DIR) _set_install_dir(picked);
        else                                          _set_game_dir(picked, FhGameDirSource.USER);
    }

    private static void _write_path_buffer(byte* buffer, string path) {
        Span<byte> span = new(buffer, _path_buffer_size);
        span.Clear();
        Encoding.UTF8.GetBytes(path.AsSpan(), span[..^1]);
    }

    private static string _read_path_buffer(byte* buffer) {
        return Marshal.PtrToStringUTF8((nint)buffer) ?? "";
    }
}
