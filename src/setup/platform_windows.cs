// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using Microsoft.Win32;

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

namespace Fahrenheit.Tools.Setup;

[SupportedOSPlatform("windows")]
internal sealed class FhWindowsPlatform : IFhSetupPlatform {
    public string name {
        get { return "Windows"; }
    }

    public List<string> get_steam_roots() {
        List<string> roots = [];

        // Steam stores its path with forward slashes, e.g. "c:/program files (x86)/steam".
        if (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string user_path) {
            roots.Add(Path.GetFullPath(user_path));
        }

        using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (RegistryKey? key = hklm.OpenSubKey(@"SOFTWARE\Valve\Steam")) {
            if (key?.GetValue("InstallPath") is string machine_path) roots.Add(machine_path);
        }

        roots.Add(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return roots;
    }

    public FhPrereqStatus[] check_prereqs(string game_dir) {
        return [
            FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X86, _get_dotnet_root("x86", Environment.SpecialFolder.ProgramFilesX86)),
            FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X64, _get_dotnet_root("x64", Environment.SpecialFolder.ProgramFiles)),
            FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X86, _get_vcredist_version(FhPrereqKind.VCREDIST_X86, RegistryView.Registry32)),
            FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X64, _get_vcredist_version(FhPrereqKind.VCREDIST_X64, RegistryView.Registry64)),
        ];
    }

    /// <summary>
    ///     The .NET installers record where they installed to in the 32-bit registry view, for either architecture.
    /// </summary>
    private static string _get_dotnet_root(string arch, Environment.SpecialFolder fallback) {
        using RegistryKey  hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        using RegistryKey? key  = hklm.OpenSubKey($@"SOFTWARE\dotnet\Setup\InstalledVersions\{arch}");

        return key?.GetValue("InstallLocation") as string
            ?? Path.Join(Environment.GetFolderPath(fallback), "dotnet");
    }

    private static Version? _get_vcredist_version(FhPrereqKind kind, RegistryView view) {
        using RegistryKey  hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using RegistryKey? key  = hklm.OpenSubKey(@"SOFTWARE\" + FhPrereqs.get_vcredist_key(kind));

        if (key == null) return null;

        return FhPrereqs.make_vcredist_version(
            _get_dword(key, "Installed"),
            _get_dword(key, "Major"),
            _get_dword(key, "Minor"),
            _get_dword(key, "Bld"));
    }

    private static uint? _get_dword(RegistryKey key, string name) {
        return key.GetValue(name) is int value ? (uint)value : null;
    }

    public string? get_install_blocker(string game_dir) {
        return null;
    }

    public string? get_shortcut_blocker() {
        return null;
    }

    public void verify_installer(string installer_path) {
        FhWinTrust.verify_authenticode(installer_path);

        // A valid signature alone is not enough; it must also be Microsoft's.
        // X509CertificateLoader has no equivalent for reading the signer of an Authenticode-signed file.
#pragma warning disable SYSLIB0057
        using X509Certificate  signed = X509Certificate.CreateFromSignedFile(installer_path);
#pragma warning restore SYSLIB0057
        using X509Certificate2 signer = new(signed);

        if (signer.GetNameInfo(X509NameType.SimpleName, forIssuer: false) != "Microsoft Corporation") {
            throw new InvalidDataException($"The installer is signed by '{signer.Subject}', not by Microsoft.");
        }
    }

    public async Task<string?> run_installer(FhPrereqInfo info, string installer_path, string game_dir, CancellationToken token) {
        ProcessStartInfo psi = new(installer_path, info.installer_args) {
            // Shell execution lets the installer raise its own elevation prompt.
            UseShellExecute = true,
        };

        Process? process;
        try {
            process = Process.Start(psi);
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223) { // ERROR_CANCELLED
            return "The administrator prompt was declined.";
        }

        if (process == null) return "The installer could not be started.";

        using (process) {
            await process.WaitForExitAsync(token);
            return _describe_installer_exit_code(process.ExitCode);
        }
    }

    /// <summary>
    ///     Both installers are WiX Burn bundles and report standard Windows Installer exit codes.
    /// </summary>
    private static string? _describe_installer_exit_code(int exit_code) {
        return exit_code switch {
            0    => null,
            1638 => null, // ERROR_PRODUCT_VERSION: a newer version is already installed.
            3010 => null, // ERROR_SUCCESS_REBOOT_REQUIRED
            1641 => null, // ERROR_SUCCESS_REBOOT_INITIATED
            1602 => "The installation was cancelled.",
            1618 => "Another installation is already in progress. Wait for it to finish, then try again.",
            _    => $"The installer failed with exit code {exit_code}.",
        };
    }

    public List<FhShortcutLocation> get_shortcut_locations(string install_dir) {
        return [
            new("install", "Fahrenheit folder", install_dir,                                                                        enabled_by_default: true),
            new("desktop", "Desktop",           Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),             enabled_by_default: true),
            new("start",   "Start menu",        Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Fahrenheit"), enabled_by_default: false),
        ];
    }

    public string create_shortcut(FhShortcutLocation location, FhLaunchEntry entry, string bin_dir, string game_dir) {
        string game_exe  = FhGameDir.find_file(game_dir, entry.exe_name) ?? Path.Join(game_dir, entry.exe_name);
        string arguments = FhLaunch.to_windows_command_line(FhLaunch.get_stage0_args(entry, game_exe));
        string lnk_path  = Path.Join(location.dir, entry.shortcut_name + ".lnk");

        Directory.CreateDirectory(location.dir);

        FhShellLink.save(new FhShellLinkInfo(
            target:      Path.Join(bin_dir, FhLaunch.STAGE0_EXE_NAME),
            arguments:   arguments,
            // Stage 0 locates the rest of Fahrenheit through its working directory.
            working_dir: bin_dir,
            description: $"Start {entry.display_name} with Fahrenheit",
            icon_path:   game_exe),
            lnk_path);

        return lnk_path;
    }
}
