// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using Fahrenheit.Tools.Setup;

namespace Fahrenheit.Tests;

[TestFixture]
public class FhSetupVdfTests {

    [Test]
    public void parses_nested_keys_and_escapes() {
        const string text = """
            // a comment
            "libraryfolders"
            {
                "0"
                {
                    "path"      "C:\\Program Files (x86)\\Steam"
                    "label"     "say \"hi\""
                    "apps"
                    {
                        "359870"    "123"
                    }
                }
            }
            """;

        FhVdfNode? folder = FhVdf.parse(text).get("libraryfolders")?.get("0");

        Assert.That(folder,                                  Is.Not.Null);
        Assert.That(folder!.get_value("path"),               Is.EqualTo(@"C:\Program Files (x86)\Steam"));
        Assert.That(folder.get_value("label"),               Is.EqualTo("say \"hi\""));
        Assert.That(folder.get("APPS")?.get_value("359870"), Is.EqualTo("123"), "keys are case-insensitive");
    }

    [TestCase("\"a\" { \"b\" \"c\"")]
    [TestCase("\"a\" \"b\" }")]
    [TestCase("\"a")]
    [TestCase("\"a\"")]
    public void rejects_malformed_documents(string text) {
        Assert.Throws<FormatException>(() => FhVdf.parse(text));
    }
}

[TestFixture]
public class FhSetupSteamTests {
    private string _root = "";

    [SetUp]
    public void set_up() {
        _root = Directory.CreateTempSubdirectory("fhsetup-test-").FullName;
    }

    [TearDown]
    public void tear_down() {
        Directory.Delete(_root, recursive: true);
    }

    private static string _make_game_dir(string library, string install_dir) {
        string dir = Path.Join(library, "steamapps", "common", install_dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, "FFX.exe"),   "");
        File.WriteAllText(Path.Join(dir, "FFX-2.exe"), "");
        return dir;
    }

    private static void _write(string path, string text) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string _vdf_escape(string path) {
        return path.Replace(@"\", @"\\");
    }

    [Test]
    public void finds_libraries_in_current_and_old_formats() {
        string steam = Path.Join(_root, "steam");
        string lib_a = Path.Join(_root, "lib_a");
        string lib_b = Path.Join(_root, "lib_b");

        _write(Path.Join(steam, "steamapps", "libraryfolders.vdf"), $$"""
            "libraryfolders"
            {
                "contentstatsid"    "123"
                "0" { "path" "{{_vdf_escape(steam)}}" }
                "1" { "path" "{{_vdf_escape(lib_a)}}" }
                "2" "{{_vdf_escape(lib_b)}}"
            }
            """);

        Assert.That(FhSteam.get_library_paths(steam), Is.EqualTo(new[] { steam, lib_a, lib_b }),
            "the Steam root must be listed exactly once, first");
    }

    [Test]
    public void finds_game_through_app_manifest_install_dir() {
        string steam    = Path.Join(_root, "steam");
        string game_dir = _make_game_dir(steam, "Some Renamed Folder");

        _write(Path.Join(steam, "steamapps", $"appmanifest_{FhGameDir.STEAM_APP_ID}.acf"), """
            "AppState"
            {
                "appid"         "359870"
                "installdir"    "Some Renamed Folder"
            }
            """);

        List<FhSteamGame> games = FhSteam.find_game([ steam ]);

        Assert.That(games,                 Has.Count.EqualTo(1));
        Assert.That(games[0].game_dir,     Is.EqualTo(Path.GetFullPath(game_dir)));
        Assert.That(games[0].library_path, Is.EqualTo(steam));
    }

    [Test]
    public void finds_game_under_default_install_dir_without_manifest() {
        string steam    = Path.Join(_root, "steam");
        string game_dir = _make_game_dir(steam, FhGameDir.STEAM_INSTALL_DIR);

        List<FhSteamGame> games = FhSteam.find_game([ steam, steam, Path.Join(_root, "missing") ]);

        Assert.That(games.Select(game => game.game_dir), Is.EqualTo(new[] { Path.GetFullPath(game_dir) }),
            "duplicate and missing roots must not produce duplicates or errors");
    }

    [Test]
    public void ignores_incomplete_game_dirs() {
        string steam    = Path.Join(_root, "steam");
        string game_dir = _make_game_dir(steam, FhGameDir.STEAM_INSTALL_DIR);
        File.Delete(Path.Join(game_dir, "FFX-2.exe"));

        Assert.That(FhSteam.find_game([ steam ]), Is.Empty);

        FhGameDirInfo info = FhGameDir.inspect(game_dir);
        Assert.That(info.is_valid, Is.False);
        Assert.That(info.ffx_exe,  Is.Not.Null);
        Assert.That(info.ffx2_exe, Is.Null);
    }

    [Test]
    public void finds_game_executables_regardless_of_case() {
        string dir = Path.Join(_root, "game");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Join(dir, "ffx.EXE"),   "");
        File.WriteAllText(Path.Join(dir, "Ffx-2.exe"), "");

        Assert.That(FhGameDir.inspect(dir).is_valid, Is.True);
    }

    [Test]
    public void derives_proton_prefix_from_game_dir() {
        string library  = Path.Join(_root, "lib");
        string game_dir = Path.Join(library, "steamapps", "common", FhGameDir.STEAM_INSTALL_DIR) + Path.DirectorySeparatorChar;

        Assert.That(FhSteam.get_proton_prefix(game_dir),
            Is.EqualTo(Path.Join(Path.GetFullPath(library), "steamapps", "compatdata", "359870", "pfx")));
    }
}

[TestFixture]
public class FhSetupPrereqTests {
    private string _root = "";

    [SetUp]
    public void set_up() {
        _root = Directory.CreateTempSubdirectory("fhsetup-test-").FullName;
    }

    [TearDown]
    public void tear_down() {
        Directory.Delete(_root, recursive: true);
    }

    private void _make_runtime(string version, bool complete = true) {
        string dir = Path.Join(_root, "shared", "Microsoft.NETCore.App", version);
        Directory.CreateDirectory(dir);
        if (complete) File.WriteAllText(Path.Join(dir, "System.Private.CoreLib.dll"), "");
    }

    [Test]
    public void finds_newest_matching_dotnet_runtime() {
        _make_runtime("9.0.21");
        _make_runtime("10.0.8");
        _make_runtime("10.0.13");
        _make_runtime("10.0.20", complete: false); // uninstall leftover
        _make_runtime("11.0.0-preview.1.26000.1");

        FhPrereqStatus status = FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X86, _root);

        Assert.That(status.state,   Is.EqualTo(FhPrereqState.INSTALLED));
        Assert.That(status.version, Is.EqualTo(new Version(10, 0, 13)));
    }

    [Test]
    public void reports_missing_dotnet_runtime() {
        _make_runtime("9.0.21");

        Assert.That(FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X64, _root).state,                       Is.EqualTo(FhPrereqState.MISSING));
        Assert.That(FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X64, Path.Join(_root, "nowhere")).state, Is.EqualTo(FhPrereqState.MISSING));
        Assert.That(FhPrereqs.check_dotnet(FhPrereqKind.DOTNET_X64, null).state,                         Is.EqualTo(FhPrereqState.MISSING));
    }

    [TestCase("10.0.13",               10, 0, 13)]
    [TestCase("10.0.0-rc.2.25502.107", 10, 0, 0)]
    [TestCase("10.0.1+abcdef",         10, 0, 1)]
    public void parses_runtime_versions(string text, int major, int minor, int build) {
        Assert.That(FhPrereqs.parse_version(text), Is.EqualTo(new Version(major, minor, build)));
    }

    [Test]
    public void classifies_vcredist_versions() {
        Assert.That(FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X64, null).state,                       Is.EqualTo(FhPrereqState.MISSING));
        Assert.That(FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X64, new Version(14, 44, 35211)).state, Is.EqualTo(FhPrereqState.OUTDATED));
        Assert.That(FhPrereqs.check_vcredist(FhPrereqKind.VCREDIST_X64, new Version(14, 50, 35719)).state, Is.EqualTo(FhPrereqState.INSTALLED));
        Assert.That(FhPrereqs.make_vcredist_version(0, 14, 50, 1), Is.Null, "Installed=0 means not installed");
    }

    [Test]
    public void reads_vcredist_from_wine_registry() {
        const string system_reg = """
            WINE REGISTRY Version 2
            ;; All keys relative to \\Machine

            [Software\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64] 1760000000
            #time=1dc3b
            "Bld"=dword:00008d97
            "Installed"=dword:00000001
            "Major"=dword:0000000e
            "Minor"=dword:00000033
            "Version"="v14.51.36247.00"

            [Software\\Wow6432Node\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\X86] 1760000000
            "Installed"=dword:00000001
            "Major"=dword:0000000e
            "Minor"=dword:00000024
            """;

        Assert.That(FhPrereqs.read_wine_vcredist(system_reg, FhPrereqKind.VCREDIST_X64), Is.EqualTo(new Version(14, 51, 36247)));
        Assert.That(FhPrereqs.read_wine_vcredist(system_reg, FhPrereqKind.VCREDIST_X86), Is.EqualTo(new Version(14, 36, 0)));
        Assert.That(FhPrereqs.read_wine_vcredist("",         FhPrereqKind.VCREDIST_X86), Is.Null);
    }

    [TestCase("https://aka.ms/vc14/vc_redist.x64.exe",                                       true)]
    [TestCase("https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.13/x.exe",            true)]
    [TestCase("https://DOWNLOAD.visualstudio.microsoft.com/download/pr/x/VC_redist.x86.exe", true)]
    [TestCase("http://aka.ms/vc14/vc_redist.x64.exe",                                        false)]
    [TestCase("https://builds.dotnet.microsoft.com.example.com/x.exe",                       false)]
    [TestCase("https://example.com/aka.ms/x.exe",                                            false)]
    public void accepts_only_microsoft_https_downloads(string url, bool expected) {
        Assert.That(FhPrereqs.is_trusted_download(new Uri(url)), Is.EqualTo(expected));
    }

    [Test]
    public void every_prerequisite_downloads_from_a_trusted_location() {
        foreach (FhPrereqInfo info in FhPrereqs.all) {
            Assert.That(FhPrereqs.is_trusted_download(new Uri(info.download_url)), info.display_name);
        }

        Assert.That(FhPrereqs.all.Select(info => info.kind), Is.EquivalentTo(Enum.GetValues<FhPrereqKind>()));
    }
}

[TestFixture]
public class FhSetupLaunchTests {
    private static FhLaunchEntry _entry(string id) {
        return FhLaunch.entries.Single(entry => entry.id == id);
    }

    [Test]
    public void passes_last_mission_flag_through_to_the_game() {
        Assert.That(FhLaunch.get_stage0_args(_entry("ffx"),    "FFX.exe"),   Is.EqualTo(new[] { "FFX.exe" }));
        Assert.That(FhLaunch.get_stage0_args(_entry("ffx2lm"), "FFX-2.exe"), Is.EqualTo(new[] { "FFX-2.exe", "--", "FFX2_LASTMISSION" }));
    }

    [TestCase(new[] { @"C:\Games\FFX.exe" }, @"C:\Games\FFX.exe")]
    [TestCase(new[] { @"C:\Steam\common\FINAL FANTASY FFX&FFX-2 HD Remaster\FFX-2.exe", "--", "FFX2_LASTMISSION" },
                      @"""C:\Steam\common\FINAL FANTASY FFX&FFX-2 HD Remaster\FFX-2.exe"" -- FFX2_LASTMISSION")]
    [TestCase(new[] { @"C:\dir with space\" }, @"""C:\dir with space\\""")]
    [TestCase(new[] { @"say ""hi""" },         @"""say \""hi\""""")]
    [TestCase(new[] { "" },                    @"""""")]
    public void quotes_windows_command_lines(string[] args, string expected) {
        Assert.That(FhLaunch.to_windows_command_line(args), Is.EqualTo(expected));
    }

    [Test]
    public void maps_unix_paths_to_wine_paths() {
        Assert.That(FhLaunch.to_wine_path("/home/deck/Games/FFX.exe"), Is.EqualTo("Z:/home/deck/Games/FFX.exe"));
        Assert.Throws<ArgumentException>(() => FhLaunch.to_wine_path("relative/FFX.exe"));
    }

    [TestCase("/home/deck/FINAL FANTASY FFX&FFX-2 HD Remaster/FFX.exe", "\"/home/deck/FINAL FANTASY FFX&FFX-2 HD Remaster/FFX.exe\"")]
    [TestCase("100%",                                                    "\"100%%\"")]
    [TestCase("a$b`c\"d",                                                @"""a\\$b\\`c\\""d""")]
    [TestCase(@"C:\x",                                                   @"""C:\\\\x""")]
    public void quotes_desktop_entry_exec_arguments(string arg, string expected) {
        Assert.That(FhLaunch.quote_desktop_exec_arg(arg), Is.EqualTo(expected));
    }

    [Test]
    public void builds_desktop_entries() {
        string entry = FhLaunch.make_desktop_entry(
            "Final Fantasy X (Fahrenheit)",
            [ "/usr/bin/protontricks-launch", "--appid", "359870", "/opt/fh/bin/fhstage0.exe", "Z:/games/FFX.exe" ],
            "/opt/fh/bin");

        Assert.That(entry, Does.StartWith("[Desktop Entry]\n"));
        Assert.That(entry, Does.Contain("Name=Final Fantasy X (Fahrenheit)\n"));
        Assert.That(entry, Does.Contain("Exec=\"/usr/bin/protontricks-launch\" \"--appid\" \"359870\" \"/opt/fh/bin/fhstage0.exe\" \"Z:/games/FFX.exe\"\n"));
        Assert.That(entry, Does.Contain("Path=/opt/fh/bin\n"));
    }

    [Test]
    public void shortcut_names_are_valid_file_names_everywhere() {
        char[] invalid_on_windows = [ '<', '>', ':', '"', '/', '\\', '|', '?', '*' ];

        foreach (FhLaunchEntry entry in FhLaunch.entries) {
            Assert.That(entry.shortcut_name.IndexOfAny(invalid_on_windows), Is.EqualTo(-1), entry.shortcut_name);
        }

        Assert.That(FhLaunch.entries.Select(entry => entry.shortcut_name).Distinct().Count(), Is.EqualTo(FhLaunch.entries.Length));
    }
}
