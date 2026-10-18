// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of Fahrenheit, © 2023-2026 The Fahrenheit contributors.
// It is licensed to you under the GNU Lesser General Public License, version 3.0 or later. See COPYING, COPYING.LESSER.

using System.IO.Compression;

using Fahrenheit.Tools.Setup;

namespace Fahrenheit.Tests;

[TestFixture]
public class FhSetupReleaseTests {

    private const string _releases_json = """
        [
            { "tag_name": "v1.0.0-alpha13", "draft": true, "prerelease": true,
              "assets": [ { "name": "fahrenheit_release_v1.0.0-alpha13.zip", "browser_download_url": "https://github.com/x/draft.zip", "size": 1 } ] },
            { "tag_name": "v1.0.0-alpha12", "draft": false, "prerelease": true,
              "assets": [
                { "name": "fhsetup.exe", "browser_download_url": "https://github.com/x/fhsetup.exe", "size": 2 },
                { "name": "fahrenheit_release_v1.0.0-alpha12.zip", "browser_download_url": "https://github.com/x/alpha12.zip", "size": 3,
                  "digest": "sha256:ac51291edf0483f0f47be7c24b50bb2169c566722f384098d62433477674dc89" } ] },
            { "tag_name": "v1.0.0-alpha11", "draft": false, "prerelease": true,
              "assets": [ { "name": "fahrenheit_release_v1.0.0-alpha11.zip", "browser_download_url": "https://github.com/x/alpha11.zip", "size": 4 } ] }
        ]
        """;

    [Test]
    public void picks_newest_published_release_including_prereleases() {
        FhRelease? release = FhReleases.parse_latest(_releases_json);

        Assert.That(release,          Is.Not.Null);
        Assert.That(release!.tag,     Is.EqualTo("v1.0.0-alpha12"), "drafts are skipped, pre-releases are not");
        Assert.That(release.zip_url,  Is.EqualTo("https://github.com/x/alpha12.zip"), "only the release ZIP is picked");
        Assert.That(release.zip_size, Is.EqualTo(3));
        Assert.That(release.sha256,   Is.EqualTo("ac51291edf0483f0f47be7c24b50bb2169c566722f384098d62433477674dc89"));
        Assert.That(release.version,  Is.EqualTo("1.0.0-alpha12"));
    }

    [Test]
    public void reports_no_release_without_a_release_zip() {
        Assert.That(FhReleases.parse_latest("[]"), Is.Null);
        Assert.That(FhReleases.parse_latest("""[ { "tag_name": "v1", "draft": false, "assets": [ { "name": "notes.txt", "browser_download_url": "https://github.com/x", "size": 1 } ] } ]"""), Is.Null);
    }

    [TestCase("v1.0.0-alpha12",          "1.0.0-alpha12")]
    [TestCase("1.0.0-alpha12+ccd352469", "1.0.0-alpha12")]
    [TestCase("1.0.0",                   "1.0.0")]
    public void normalizes_versions(string version, string expected) {
        Assert.That(FhReleases.normalize_version(version), Is.EqualTo(expected));
    }

    [TestCase("https://api.github.com/repos/fahrenheit-crew/fahrenheit/releases",                         true)]
    [TestCase("https://github.com/fahrenheit-crew/fahrenheit/releases/download/v1/a.zip",                true)]
    [TestCase("https://release-assets.githubusercontent.com/github-production-release-asset/1/2",        true)]
    [TestCase("http://github.com/fahrenheit-crew/fahrenheit/releases/download/v1/a.zip",                 false)]
    [TestCase("https://github.com.example.com/a.zip",                                                     false)]
    public void accepts_only_github_https_locations(string url, bool expected) {
        Assert.That(FhReleases.is_trusted(new Uri(url)), Is.EqualTo(expected));
    }
}

[TestFixture]
public class FhSetupInstallerTests {
    private string _root = "";

    [SetUp]
    public void set_up() {
        _root = Directory.CreateTempSubdirectory("fhsetup-test-").FullName;
    }

    [TearDown]
    public void tear_down() {
        Directory.Delete(_root, recursive: true);
    }

    private string _make_zip(string name, params (string path, string content)[] entries) {
        string zip_path = Path.Join(_root, name);
        using ZipArchive zip = ZipFile.Open(zip_path, ZipArchiveMode.Create);

        foreach ((string path, string content) in entries) {
            using StreamWriter writer = new(zip.CreateEntry(path).Open());
            writer.Write(content);
        }

        return zip_path;
    }

    private static void _write(string path, string text) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Test]
    public void classifies_install_dirs() {
        string empty = Path.Join(_root, "empty");
        Directory.CreateDirectory(empty);
        _write(Path.Join(_root, "other", "notes.txt"),          "");
        _write(Path.Join(_root, "fh", "bin", "fhstage0.exe"),   "");
        _write(Path.Join(_root, "game", "FFX.exe"),             "");
        _write(Path.Join(_root, "game", "FFX-2.exe"),           "");

        Assert.That(FhInstaller.inspect(Path.Join(_root, "missing")).state,        Is.EqualTo(FhInstallDirState.EMPTY));
        Assert.That(FhInstaller.inspect(empty).state,                              Is.EqualTo(FhInstallDirState.EMPTY));
        Assert.That(FhInstaller.inspect(Path.Join(_root, "other")).state,          Is.EqualTo(FhInstallDirState.OTHER));
        Assert.That(FhInstaller.inspect(Path.Join(_root, "fh")).state,             Is.EqualTo(FhInstallDirState.FAHRENHEIT));
        Assert.That(FhInstaller.inspect(Path.Join(_root, "fh")).version,           Is.Null, "a fake fh.dll has no version");
        Assert.That(FhInstaller.inspect(Path.Join(_root, "game")).state,           Is.EqualTo(FhInstallDirState.GAME));
        Assert.That(FhInstaller.inspect(Path.Join(_root, "game", "mods")).state,   Is.EqualTo(FhInstallDirState.GAME), "inside the game folder");
        Assert.That(FhInstaller.inspect("").state,                                 Is.EqualTo(FhInstallDirState.OTHER));
    }

    [Test]
    public void reads_the_version_of_a_real_library() {
        string library = typeof(FhInstaller).Assembly.Location;
        Assert.That(FhInstaller.read_version(library), Is.Not.Null.And.Not.Contain("+"));
    }

    [Test]
    public void update_replaces_bin_and_keeps_user_files() {
        string install = Path.Join(_root, "Fahrenheit");
        _write(Path.Join(install, "bin", "fhstage0.exe"),  "old");
        _write(Path.Join(install, "bin", "removed.dll"),   "old");
        _write(Path.Join(install, "mods", "loadorder"),    "mymod");
        _write(Path.Join(install, "saves", "save1"),       "progress");

        string zip = _make_zip("release.zip",
            ("bin/fhstage0.exe",  "new"),
            ("setup/fhsetup.exe", "new"));

        FhInstaller.install(zip, install, running_exe: null);

        Assert.That(File.ReadAllText(Path.Join(install, "bin", "fhstage0.exe")),  Is.EqualTo("new"));
        Assert.That(File.Exists(Path.Join(install, "bin", "removed.dll")),        Is.False, "files from the old release must not linger");
        Assert.That(File.ReadAllText(Path.Join(install, "setup", "fhsetup.exe")), Is.EqualTo("new"));
        Assert.That(File.ReadAllText(Path.Join(install, "mods", "loadorder")),    Is.EqualTo("mymod"));
        Assert.That(File.ReadAllText(Path.Join(install, "saves", "save1")),       Is.EqualTo("progress"));
    }

    [Test]
    public void renames_the_running_tool_instead_of_deleting_it() {
        string install = Path.Join(_root, "Fahrenheit");
        string running = Path.Join(install, "setup", "fhsetup.exe");
        _write(Path.Join(install, "bin", "fhstage0.exe"), "old");
        _write(running,                                    "old tool");

        FhInstaller.install(_make_zip("release.zip", ("bin/fhstage0.exe", "new"), ("setup/fhsetup.exe", "new tool")), install, running);

        Assert.That(File.ReadAllText(running),          Is.EqualTo("new tool"));
        Assert.That(File.ReadAllText(running + ".old"), Is.EqualTo("old tool"));

        FhInstaller.remove_leftovers(Path.GetDirectoryName(running)!);
        Assert.That(File.Exists(running + ".old"), Is.False);
    }

    [Test]
    public void rejects_files_that_are_not_releases_without_touching_the_installation() {
        string install = Path.Join(_root, "Fahrenheit");
        _write(Path.Join(install, "bin", "fhstage0.exe"), "old");

        string not_release = _make_zip("other.zip",    ("readme.txt", "x"));
        string traversal   = _make_zip("traverse.zip", ("bin/fhstage0.exe", "new"), ("../escaped.txt", "x"));

        Assert.Throws<InvalidDataException>(() => FhInstaller.install(not_release, install, null));
        Assert.Throws<InvalidDataException>(() => FhInstaller.install(traversal,   install, null));

        Assert.That(File.ReadAllText(Path.Join(install, "bin", "fhstage0.exe")), Is.EqualTo("old"));
        Assert.That(File.Exists(Path.Join(_root, "escaped.txt")),                Is.False);
    }

    [Test]
    public void finds_the_installation_a_tool_belongs_to() {
        string install = Path.Join(_root, "Fahrenheit");
        _write(Path.Join(install, "bin", "fhstage0.exe"), "");
        Directory.CreateDirectory(Path.Join(install, "setup"));
        Directory.CreateDirectory(Path.Join(_root, "Downloads"));

        Assert.That(FhInstaller.find_install_of_tool(Path.Join(install, "setup") + Path.DirectorySeparatorChar), Is.EqualTo(Path.GetFullPath(install)));
        Assert.That(FhInstaller.find_install_of_tool(Path.Join(_root, "Downloads")),                               Is.Null);
    }
}
