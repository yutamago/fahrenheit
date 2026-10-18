// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     Downloads prerequisite installers from Microsoft.
/// </summary>
internal static class FhDownloader {
    private static readonly HttpClient _http = new(new SocketsHttpHandler {
        AllowAutoRedirect        = true,
        MaxAutomaticRedirections = 10,
    }) {
        Timeout = TimeSpan.FromMinutes(15),
    };

    /// <summary>
    ///     Downloads an installer into <paramref name="dest_dir"/> and returns its path. Refuses anything that is not
    ///     served over HTTPS from a Microsoft host, including after redirects.
    /// </summary>
    /// <param name="progress">Receives the completed fraction (0 to 1), or a negative value if the size is unknown.</param>
    public static async Task<string> download(FhPrereqInfo info, string dest_dir, Action<float> progress, CancellationToken token) {
        Uri source = new(info.download_url);
        if (!FhPrereqs.is_trusted_download(source)) {
            throw new InvalidOperationException($"Refusing to download from untrusted location {source}.");
        }

        using HttpResponseMessage response = await _http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        Uri final_uri = response.RequestMessage?.RequestUri ?? source;
        if (!FhPrereqs.is_trusted_download(final_uri)) {
            throw new InvalidOperationException($"The download was redirected to untrusted location {final_uri}.");
        }

        string file_name = Path.GetFileName(final_uri.LocalPath);
        if (!file_name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
            file_name = $"{info.kind.ToString().ToLowerInvariant()}.exe";
        }

        Directory.CreateDirectory(dest_dir);
        string dest      = Path.Join(dest_dir, file_name);
        string dest_part = dest + ".part";

        long? total = response.Content.Headers.ContentLength;

        await using (Stream source_stream = await response.Content.ReadAsStreamAsync(token))
        await using (FileStream dest_stream = new(dest_part, FileMode.Create, FileAccess.Write, FileShare.None)) {
            byte[] buffer = new byte[81920];
            long   done   = 0;
            int    read;

            while ((read = await source_stream.ReadAsync(buffer, token)) > 0) {
                await dest_stream.WriteAsync(buffer.AsMemory(0, read), token);
                done += read;
                progress(total is > 0 ? (float)done / total.Value : -1f);
            }
        }

        File.Move(dest_part, dest, overwrite: true);
        return dest;
    }
}
