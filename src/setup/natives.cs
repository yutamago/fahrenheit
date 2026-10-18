// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using System.Reflection;
using System.Security.Cryptography;

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     Makes the setup tool a single file. Release builds embed the native libraries the UI needs (SDL3, cimgui
///     and the ImGui backends) and the UI font. At startup they are written to a per-user cache folder and loaded from there.
///     Builds without embedded libraries load them from next to the executable, as usual.
/// </summary>
internal static class FhEmbeddedFiles {
    private const string _native_prefix = "native/";
    private const string _font_name     = "font/NotoSans-Regular.ttf";

    /// <summary>
    ///     The order in which the native libraries are loaded. Later ones link against earlier ones,
    ///     and the dynamic loader resolves those links to the already loaded libraries.
    /// </summary>
    private static readonly string[] _load_order = [ "SDL3", "cimgui", "ImGuiImpl", "ImGuiImplSDL3" ];

    private static readonly Dictionary<string, nint> _handles = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The extracted UI font, or <see langword="null"/> if this build does not embed one.
    /// </summary>
    public static string? font_path { get; private set; }

    public static void initialize() {
        Assembly assembly  = typeof(FhEmbeddedFiles).Assembly;
        string[] resources = assembly.GetManifestResourceNames();
        if (resources.Length == 0) return;

        string cache_dir = _get_cache_dir(assembly, resources);
        Directory.CreateDirectory(cache_dir);

        Dictionary<string, string> natives = new(StringComparer.OrdinalIgnoreCase);

        foreach (string resource in resources) {
            string path = Path.Join(cache_dir, Path.GetFileName(resource));
            _extract(assembly, resource, path);

            if (resource.StartsWith(_native_prefix, StringComparison.Ordinal)) natives[_get_library_stem(resource)] = path;
            if (resource == _font_name)                                         font_path = path;
        }

        foreach (string stem in _load_order) {
            if (natives.TryGetValue(stem, out string? path)) _handles[stem] = NativeLibrary.Load(path);
        }

        LibraryLoader.InterceptLibraryLoad += _intercept;
    }

    private static bool _intercept(string library_name, out nint handle) {
        return _handles.TryGetValue(_get_library_stem(library_name), out handle);
    }

    /// <summary>
    ///     Reduces <c>native/libSDL3.so</c>, <c>SDL3.dll</c> or <c>SDL3</c> to <c>SDL3</c>.
    /// </summary>
    private static string _get_library_stem(string name) {
        string stem = Path.GetFileName(name);

        foreach (string extension in (string[])[ ".dll", ".so", ".dylib" ]) {
            int index = stem.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (index > 0) stem = stem[..index];
        }

        return stem.StartsWith("lib", StringComparison.Ordinal) ? stem[3..] : stem;
    }

    /// <summary>
    ///     A per-user folder, so other users cannot plant libraries in it, named after the embedded content,
    ///     so different versions of the tool do not share files.
    /// </summary>
    private static string _get_cache_dir(Assembly assembly, string[] resources) {
        string base_dir = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            : Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg_cache
                ? xdg_cache
                : Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string resource in resources.Order(StringComparer.Ordinal)) {
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            hash.AppendData(Encoding.UTF8.GetBytes(resource));
            hash.AppendData(BitConverter.GetBytes(stream.Length));
        }

        return Path.Join(base_dir, "fhsetup", Convert.ToHexStringLower(hash.GetHashAndReset())[..16]);
    }

    /// <summary>
    ///     Writes an embedded file unless an identical copy already exists, e.g. from an earlier run.
    /// </summary>
    private static void _extract(Assembly assembly, string resource, string path) {
        using Stream source = assembly.GetManifestResourceStream(resource)!;
        byte[] expected     = SHA256.HashData(source);

        if (File.Exists(path)) {
            using FileStream existing = File.OpenRead(path);
            if (SHA256.HashData(existing).AsSpan().SequenceEqual(expected)) return;
        }

        source.Position = 0;
        string part = path + ".part";
        using (FileStream dest = new(part, FileMode.Create, FileAccess.Write, FileShare.None)) {
            source.CopyTo(dest);
        }
        File.Move(part, path, overwrite: true);
    }
}
