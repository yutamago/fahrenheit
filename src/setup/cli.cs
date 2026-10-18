// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     A command-line interface for scripted or headless setups. Without arguments, the setup window opens instead.
/// </summary>
internal static class FhSetupCli {
    private const string _usage = """
        Usage: fhsetup [options]

        Without options, opens the setup window.

        Options:
          --check                       Print the detected game folder and the prerequisite status.
          --game-dir <path>             Use this game folder instead of the one found in Steam.
          --install-prereqs             Download and install missing prerequisites.
          --create-shortcuts [ids]      Create shortcuts in the given comma-separated locations
                                        (default: the locations enabled in the setup window).
          --help                        Show this help.

        Exit code 0 means every requested step succeeded and all prerequisites are installed.
        """;

    public static int run(FhSetup setup, string[] args) {
        bool    check            = false;
        bool    install_prereqs  = false;
        bool    create_shortcuts = false;
        string? locations        = null;
        string? game_dir         = null;

        for (int i = 0; i < args.Length; i++) {
            switch (args[i]) {
                case "--check":           check           = true; break;
                case "--install-prereqs": install_prereqs = true; break;
                case "--game-dir" when i + 1 < args.Length:
                    game_dir = args[++i];
                    break;
                case "--create-shortcuts":
                    create_shortcuts = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) locations = args[++i];
                    break;
                case "--help" or "-h" or "/?":
                    Console.WriteLine(_usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"Unknown option '{args[i]}'.");
                    Console.Error.WriteLine(_usage);
                    return 2;
            }
        }

        setup.detect();
        if (game_dir != null) setup.set_game_dir(game_dir, FhGameDirSource.USER);

        bool ok = true;

        if (install_prereqs) {
            if (setup.install_blocker != null) {
                Console.Error.WriteLine(setup.install_blocker);
                ok = false;
            }
            else {
                setup.install_missing_prereqs().GetAwaiter().GetResult();
            }
        }

        if (create_shortcuts) {
            if (locations != null) {
                string[] ids = locations.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                for (int l = 0; l < setup.shortcut_locations.Count; l++) {
                    setup.location_enabled[l] = Array.IndexOf(ids, setup.shortcut_locations[l].id) >= 0;
                }
            }

            ok &= setup.finish();
        }

        if (check || install_prereqs || create_shortcuts) print_status(setup);

        foreach (FhLogLine line in setup.get_log()) {
            (line.level == FhLogLevel.ERROR ? Console.Error : Console.Out).WriteLine($"[{line.level}] {line.text}");
        }

        ok &= setup.game_info.is_valid && setup.missing_prereqs.Length == 0;
        return ok ? 0 : 1;
    }

    private static void print_status(FhSetup setup) {
        Console.WriteLine($"Platform:      {setup.platform.name}");
        Console.WriteLine($"Fahrenheit:    {setup.install_dir}{(setup.is_in_install_dir ? "" : " (bin\\fhstage0.exe not found!)")}");
        Console.WriteLine($"Game folder:   {(setup.game_dir.Length == 0 ? "(not found)" : setup.game_dir)} [{setup.game_dir_source}, {(setup.game_info.is_valid ? "valid" : "invalid")}]");

        foreach (FhPrereqStatus status in setup.prereqs) {
            Console.WriteLine($"  {FhPrereqs.get(status.kind).display_name,-36} {status.state,-10} {status.version?.ToString() ?? ""}");
        }

        if (setup.install_blocker != null) Console.WriteLine($"Note: {setup.install_blocker}");

        Console.WriteLine("Shortcut locations:");
        foreach (FhShortcutLocation location in setup.shortcut_locations) {
            Console.WriteLine($"  {location.id,-8} {location.display_name,-18} {location.dir}");
        }
    }
}
