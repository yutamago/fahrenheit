// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

namespace Fahrenheit.Tools.Setup;

/*
 * The setup tool is published with NativeAOT, which does not support the built-in COM interop
 * (`[ComImport]` interfaces, `dynamic` COM objects). COM is used through source-generated wrappers
 * (`[GeneratedComInterface]`) instead, and plain Win32 calls through `[LibraryImport]`.
 */

/// <summary>
///     The properties of a shell link (<c>.lnk</c> file).
/// </summary>
internal sealed record FhShellLinkInfo(string target, string arguments, string working_dir, string description, string icon_path);

[SupportedOSPlatform("windows")]
internal static unsafe partial class FhShellLink {
    private static readonly Guid _clsid_shell_link = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid _iid_iunknown     = new("00000000-0000-0000-C000-000000000046");

    private const uint _clsctx_inproc_server       = 0x1;
    private const uint _coinit_apartmentthreaded   = 0x2;

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(void* pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(in Guid rclsid, void* pUnkOuter, uint dwClsContext, in Guid riid, out void* ppv);

    /// <summary>
    ///     Writes a shell link to <paramref name="lnk_path"/>, replacing any existing file.
    /// </summary>
    public static void save(FhShellLinkInfo info, string lnk_path) {
        // S_FALSE (already initialized) and RPC_E_CHANGED_MODE (initialized as MTA) are both fine; the shell link supports either.
        _ = CoInitializeEx(null, _coinit_apartmentthreaded);

        Marshal.ThrowExceptionForHR(CoCreateInstance(_clsid_shell_link, null, _clsctx_inproc_server, _iid_iunknown, out void* unknown));

        try {
            StrategyBasedComWrappers wrappers = new();
            IShellLinkW link = (IShellLinkW)wrappers.GetOrCreateObjectForComInstance((nint)unknown, CreateObjectFlags.None);

            link.SetPath            (info.target);
            link.SetArguments       (info.arguments);
            link.SetWorkingDirectory(info.working_dir);
            link.SetDescription     (info.description);
            link.SetIconLocation    (info.icon_path, 0);

            ((IPersistFile)link).Save(lnk_path, fRemember: 1);
        }
        finally {
            Marshal.Release((nint)unknown);
        }
    }
}

/// <summary>
///     https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishelllinkw
///     Methods must be declared in vtable order. Unused methods keep raw pointer signatures.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal unsafe partial interface IShellLinkW {
    void GetPath            (char* pszFile, int cch, void* pfd, uint fFlags);
    void GetIDList          (void** ppidl);
    void SetIDList          (void* pidl);
    void GetDescription     (char* pszName, int cch);
    void SetDescription     (string pszName);
    void GetWorkingDirectory(char* pszDir, int cch);
    void SetWorkingDirectory(string pszDir);
    void GetArguments       (char* pszArgs, int cch);
    void SetArguments       (string pszArgs);
    void GetHotkey          (ushort* pwHotkey);
    void SetHotkey          (ushort wHotkey);
    void GetShowCmd         (int* piShowCmd);
    void SetShowCmd         (int iShowCmd);
    void GetIconLocation    (char* pszIconPath, int cch, int* piIcon);
    void SetIconLocation    (string pszIconPath, int iIcon);
    void SetRelativePath    (string pszPathRel, uint dwReserved);
    void Resolve            (nint hwnd, uint fFlags);
    void SetPath            (string pszFile);
}

/// <summary>
///     https://learn.microsoft.com/en-us/windows/win32/api/objidl/nn-objidl-ipersistfile
///     <c>GetClassID</c> is inherited from <c>IPersist</c>; it is declared here to keep the vtable layout.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0000010b-0000-0000-C000-000000000046")]
internal unsafe partial interface IPersistFile {
    void GetClassID   (Guid* pClassID);
    [PreserveSig]
    int  IsDirty      ();
    void Load         (string pszFileName, uint dwMode);
    void Save         (string pszFileName, int fRemember);
    void SaveCompleted(string pszFileName);
    void GetCurFile   (char** ppszFileName);
}

/// <summary>
///     Authenticode verification through <c>WinVerifyTrust</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class FhWinTrust {
    // WINTRUST_ACTION_GENERIC_VERIFY_V2
    private static readonly Guid _action_generic_verify_v2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    private const uint _wtd_ui_none         = 2;
    private const uint _wtd_revoke_none     = 0;
    private const uint _wtd_choice_file     = 1;
    private const uint _wtd_stateaction_ignore = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_FILE_INFO {
        public uint  cbStruct;
        public char* pcwszFilePath;
        public nint  hFile;
        public Guid* pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA {
        public uint                cbStruct;
        public void*               pPolicyCallbackData;
        public void*               pSIPClientData;
        public uint                dwUIChoice;
        public uint                fdwRevocationChecks;
        public uint                dwUnionChoice;
        public WINTRUST_FILE_INFO* pFile;
        public uint                dwStateAction;
        public nint                hWVTStateData;
        public char*               pwszURLReference;
        public uint                dwProvFlags;
        public uint                dwUIContext;
        public void*               pSignatureSettings;
    }

    [LibraryImport("wintrust.dll")]
    private static partial int WinVerifyTrust(nint hwnd, Guid* pgActionID, WINTRUST_DATA* pWVTData);

    /// <summary>
    ///     Throws if the file does not carry a valid Authenticode signature that chains to a trusted root.
    /// </summary>
    public static void verify_authenticode(string path) {
        fixed (char* p_path = path) {
            WINTRUST_FILE_INFO file_info = new() {
                cbStruct      = (uint)sizeof(WINTRUST_FILE_INFO),
                pcwszFilePath = p_path,
            };

            WINTRUST_DATA data = new() {
                cbStruct            = (uint)sizeof(WINTRUST_DATA),
                dwUIChoice          = _wtd_ui_none,
                fdwRevocationChecks = _wtd_revoke_none,
                dwUnionChoice       = _wtd_choice_file,
                pFile               = &file_info,
                dwStateAction       = _wtd_stateaction_ignore,
            };

            Guid action = _action_generic_verify_v2;
            int  result = WinVerifyTrust(-1, &action, &data); // INVALID_HANDLE_VALUE: never show UI

            if (result != 0) {
                throw new InvalidDataException($"The installer's digital signature is not valid (0x{result:X8}).");
            }
        }
    }
}
