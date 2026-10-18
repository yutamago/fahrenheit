// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     One way of starting the game with Fahrenheit, for which a shortcut can be created.
/// </summary>
/// <param name="game_arg">An argument passed through to the game, or <see langword="null"/>.</param>
public sealed record FhLaunchEntry(string id, string display_name, string exe_name, string? game_arg) {
    /// <summary>
    ///     The shortcut's file name, without extension. Must be a valid file name on every platform.
    /// </summary>
    public string shortcut_name {
        get { return $"{display_name} (Fahrenheit)"; }
    }
}

/// <summary>
///     Builds the command lines and shortcut files that start the game through the Stage 0 loader.
/// </summary>
public static class FhLaunch {
    public const string STAGE0_EXE_NAME = "fhstage0.exe";

    public static readonly FhLaunchEntry[] entries = [
        new("ffx",    "Final Fantasy X",                FhGameDir.FFX_EXE_NAME,  null),
        new("ffx2",   "Final Fantasy X-2",              FhGameDir.FFX2_EXE_NAME, null),
        new("ffx2lm", "Final Fantasy X-2 Last Mission", FhGameDir.FFX2_EXE_NAME, "FFX2_LASTMISSION"),
    ];

    /// <summary>
    ///     Builds the arguments for <c>fhstage0.exe</c>. Stage 0 passes everything after <c>--</c> on to the game.
    /// </summary>
    /// <param name="game_exe_path">The game executable's path, as the Windows (or Wine) process will see it.</param>
    public static string[] get_stage0_args(FhLaunchEntry entry, string game_exe_path) {
        return entry.game_arg == null
            ? [ game_exe_path ]
            : [ game_exe_path, "--", entry.game_arg ];
    }

    /// <summary>
    ///     Joins arguments into a Windows command line, quoting them as <c>CommandLineToArgvW</c> expects.
    /// </summary>
    public static string to_windows_command_line(IEnumerable<string> args) {
        StringBuilder sb = new();

        foreach (string arg in args) {
            if (sb.Length > 0) sb.Append(' ');

            if (arg.Length > 0 && arg.IndexOfAny([ ' ', '\t', '"', '&' ]) < 0) {
                sb.Append(arg);
                continue;
            }

            sb.Append('"');
            int backslashes = 0;
            foreach (char c in arg) {
                if (c == '\\') { backslashes++; continue; }

                // Backslashes are only special when they precede a quote.
                sb.Append('\\', c == '"' ? backslashes * 2 + 1 : backslashes);
                backslashes = 0;
                sb.Append(c);
            }
            sb.Append('\\', backslashes * 2);
            sb.Append('"');
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Maps an absolute Unix path to the path a Wine/Proton process sees, through Wine's <c>Z:</c> drive,
    ///     which maps the Unix root directory. Forward slashes are kept, since Windows accepts both separators.
    /// </summary>
    public static string to_wine_path(string unix_path) {
        if (!unix_path.StartsWith('/')) throw new ArgumentException($"'{unix_path}' is not an absolute Unix path.", nameof(unix_path));
        return "Z:" + unix_path;
    }

    /// <summary>
    ///     Quotes one argument for the <c>Exec</c> key of a desktop entry file.
    ///     See https://specifications.freedesktop.org/desktop-entry-spec/latest/exec-variables.html.
    /// </summary>
    public static string quote_desktop_exec_arg(string arg) {
        StringBuilder sb = new("\"");

        foreach (char c in arg) {
            // Inside a quoted argument, these must be escaped with a backslash.
            if (c is '"' or '`' or '$' or '\\') sb.Append('\\');
            // A literal '%' must be doubled, or it is read as a field code.
            if (c == '%') sb.Append('%');
            sb.Append(c);
        }

        sb.Append('"');

        // The string-level escape rules are applied before the quoting rules, so every backslash is doubled once more.
        return sb.ToString().Replace(@"\", @"\\");
    }

    /// <summary>
    ///     Builds a desktop entry file that runs <paramref name="exec_args"/> in <paramref name="working_dir"/>.
    /// </summary>
    public static string make_desktop_entry(string name, IEnumerable<string> exec_args, string working_dir, string? comment = null) {
        StringBuilder sb = new();

        sb.Append("[Desktop Entry]\n");
        sb.Append("Type=Application\n");
        sb.Append("Version=1.5\n");
        sb.Append($"Name={_escape_desktop_string(name)}\n");
        if (comment != null) sb.Append($"Comment={_escape_desktop_string(comment)}\n");
        sb.Append($"Exec={string.Join(' ', exec_args.Select(quote_desktop_exec_arg))}\n");
        sb.Append($"Path={_escape_desktop_string(working_dir)}\n");
        sb.Append("Terminal=false\n");
        sb.Append("Categories=Game;\n");

        return sb.ToString();
    }

    private static string _escape_desktop_string(string value) {
        return value
            .Replace(@"\", @"\\")
            .Replace("\n", @"\n")
            .Replace("\t", @"\t")
            .Replace("\r", @"\r");
    }
}
