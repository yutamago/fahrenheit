// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using System.Security.Cryptography;

namespace Fahrenheit.Tools.Setup;

/// <summary>
///     Downloads prerequisite installers from Microsoft and Fahrenheit releases from GitHub.
/// </summary>
internal static class FhDownloader {
    private static readonly HttpClient _http = _create_client();

    private static HttpClient _create_client() {
        HttpClient http = new(new SocketsHttpHandler {
            AllowAutoRedirect        = true,
            MaxAutomaticRedirections = 10,
        }) {
            Timeout = TimeSpan.FromMinutes(15),
        };

        // The GitHub API rejects requests without a user agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("fhsetup");
        return http;
    }

    /// <summary>
    ///     Downloads a prerequisite installer into <paramref name="dest_dir"/> and returns its path.
    ///     Only HTTPS downloads from Microsoft hosts are accepted, including after redirects.
    /// </summary>
    /// <param name="progress">Receives the completed fraction (0 to 1), or a negative value if the size is unknown.</param>
    public static async Task<string> download(FhPrereqInfo info, string dest_dir, Action<float> progress, CancellationToken token) {
        return await download_file(new Uri(info.download_url), FhPrereqs.is_trusted_download, dest_dir,
            $"{info.kind.ToString().ToLowerInvariant()}.exe", null, progress, token);
    }

    /// <summary>
    ///     Reads a text response, such as a JSON API reply. The request and every redirect must pass <paramref name="is_trusted"/>.
    /// </summary>
    public static async Task<string> get_string(Uri source, Func<Uri, bool> is_trusted, CancellationToken token) {
        using HttpResponseMessage response = await _get(source, is_trusted, token);
        return await response.Content.ReadAsStringAsync(token);
    }

    /// <summary>
    ///     Downloads a file into <paramref name="dest_dir"/> and returns its path.
    /// </summary>
    /// <param name="fallback_name">The file name to use if the final URL does not end in a file name.</param>
    /// <param name="expected_sha256">If not <see langword="null"/>, the download is rejected unless its SHA-256 digest matches.</param>
    public static async Task<string> download_file(
        Uri source, Func<Uri, bool> is_trusted, string dest_dir, string fallback_name,
        string? expected_sha256, Action<float> progress, CancellationToken token)
    {
        using HttpResponseMessage response = await _get(source, is_trusted, token);

        Uri    final_uri = response.RequestMessage?.RequestUri ?? source;
        string file_name = Path.GetFileName(final_uri.LocalPath);
        if (string.IsNullOrEmpty(Path.GetExtension(file_name))) file_name = fallback_name;

        Directory.CreateDirectory(dest_dir);
        string dest      = Path.Join(dest_dir, file_name);
        string dest_part = dest + ".part";

        long? total = response.Content.Headers.ContentLength;

        using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await using (Stream source_stream = await response.Content.ReadAsStreamAsync(token))
        await using (FileStream dest_stream = new(dest_part, FileMode.Create, FileAccess.Write, FileShare.None)) {
            byte[] buffer = new byte[81920];
            long   done   = 0;
            int    read;

            while ((read = await source_stream.ReadAsync(buffer, token)) > 0) {
                await dest_stream.WriteAsync(buffer.AsMemory(0, read), token);
                sha256.AppendData(buffer, 0, read);
                done += read;
                progress(total is > 0 ? (float)done / total.Value : -1f);
            }
        }

        string actual_sha256 = Convert.ToHexStringLower(sha256.GetHashAndReset());
        if (expected_sha256 != null && !actual_sha256.Equals(expected_sha256, StringComparison.OrdinalIgnoreCase)) {
            File.Delete(dest_part);
            throw new InvalidDataException($"The download is corrupt: its SHA-256 digest is {actual_sha256}, expected {expected_sha256}.");
        }

        File.Move(dest_part, dest, overwrite: true);
        return dest;
    }

    private static async Task<HttpResponseMessage> _get(Uri source, Func<Uri, bool> is_trusted, CancellationToken token) {
        if (!is_trusted(source)) {
            throw new InvalidOperationException($"Refusing to download from untrusted location {source}.");
        }

        HttpResponseMessage response = await _http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, token);

        try {
            response.EnsureSuccessStatusCode();

            Uri final_uri = response.RequestMessage?.RequestUri ?? source;
            if (!is_trusted(final_uri)) {
                throw new InvalidOperationException($"The download was redirected to untrusted location {final_uri.GetLeftPart(UriPartial.Path)}.");
            }

            return response;
        }
        catch {
            response.Dispose();
            throw;
        }
    }
}
