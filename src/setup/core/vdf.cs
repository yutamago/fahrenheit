// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     A node in a text VDF (Valve KeyValues) document. A node holds either a string value or child nodes.
/// </summary>
public sealed class FhVdfNode {
    private readonly List<KeyValuePair<string, FhVdfNode>> _children = [];

    public string? value { get; }

    public FhVdfNode(string? value = null) {
        this.value = value;
    }

    public IReadOnlyList<KeyValuePair<string, FhVdfNode>> children {
        get { return _children; }
    }

    internal void add(string key, FhVdfNode node) {
        _children.Add(new(key, node));
    }

    /// <summary>
    ///     Gets the first child with the given key. Keys are compared case-insensitively, as Steam does.
    /// </summary>
    public FhVdfNode? get(string key) {
        foreach ((string child_key, FhVdfNode child) in _children) {
            if (child_key.Equals(key, StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    /// <summary>
    ///     Gets the string value of the first child with the given key.
    /// </summary>
    public string? get_value(string key) {
        return get(key)?.value;
    }
}

/// <summary>
///     Parses the text VDF format used by Steam's configuration files (<c>libraryfolders.vdf</c>, <c>*.acf</c>, ...).
/// </summary>
public static class FhVdf {

    /// <summary>
    ///     Parses a text VDF document. Returns a synthetic root node whose children are the document's top-level keys.
    /// </summary>
    public static FhVdfNode parse(string text) {
        int       pos  = 0;
        FhVdfNode root = new();
        _parse_children(text, ref pos, root, top_level: true);
        return root;
    }

    private static void _parse_children(string text, ref int pos, FhVdfNode parent, bool top_level) {
        while (true) {
            _skip_trivia(text, ref pos);

            if (pos >= text.Length) {
                if (!top_level) throw new FormatException("Unexpected end of VDF document; missing '}'.");
                return;
            }

            if (text[pos] == '}') {
                if (top_level) throw new FormatException($"Unexpected '}}' at offset {pos}.");
                pos++;
                return;
            }

            string key = _read_token(text, ref pos);
            _skip_trivia(text, ref pos);

            if (pos >= text.Length) throw new FormatException($"Key '{key}' has no value.");

            if (text[pos] == '{') {
                pos++;
                FhVdfNode child = new();
                _parse_children(text, ref pos, child, top_level: false);
                parent.add(key, child);
            }
            else {
                parent.add(key, new FhVdfNode(_read_token(text, ref pos)));
            }
        }
    }

    private static void _skip_trivia(string text, ref int pos) {
        while (pos < text.Length) {
            char c = text[pos];

            if (char.IsWhiteSpace(c)) {
                pos++;
            }
            else if (c == '/' && pos + 1 < text.Length && text[pos + 1] == '/') {
                while (pos < text.Length && text[pos] != '\n') pos++;
            }
            else if (c == '[') {
                // Conditionals such as [$WIN32]. They are not used in the files we read, so they are skipped.
                while (pos < text.Length && text[pos] != ']') pos++;
                pos++;
            }
            else {
                return;
            }
        }
    }

    private static string _read_token(string text, ref int pos) {
        StringBuilder sb = new();

        if (text[pos] != '"') {
            while (pos < text.Length && !char.IsWhiteSpace(text[pos]) && text[pos] is not ('{' or '}' or '"')) {
                sb.Append(text[pos++]);
            }
            return sb.ToString();
        }

        pos++; // opening quote
        while (true) {
            if (pos >= text.Length) throw new FormatException("Unterminated string in VDF document.");

            char c = text[pos++];
            if (c == '"') return sb.ToString();

            if (c == '\\' && pos < text.Length) {
                char escaped = text[pos++];
                sb.Append(escaped switch {
                    'n' => '\n',
                    't' => '\t',
                    _   => escaped, // \\ and \"
                });
                continue;
            }

            sb.Append(c);
        }
    }
}
