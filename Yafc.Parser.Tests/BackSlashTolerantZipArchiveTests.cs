using System.IO;
using System.IO.Compression;
using System.Text;

namespace Yafc.Parser.Tests;

public class BackslashTolerantZipArchiveTests : IDisposable {
    static BackslashTolerantZipArchiveTests() {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) {
            // clean entry
            var clean = archive.CreateEntry("mod/info.json");
            using (var s = clean.Open())
                s.Write("clean"u8);
            // dirty entry - using reflection to do rename to our testcase because ZipArchive normalizes FullName (unlike powershell's compress-archive)
            var dirty = archive.CreateEntry("PLACEHOLDER");
            using (var s = dirty.Open())
                s.Write("dirty"u8);
            RenameEntry(dirty, "asecretfolder\\info.json");
        }
        ms.Position = 0;
        TestZip = ms;
    }

    public void Dispose() {
        TestZip.Position = 0;
    }

    /**
    ZipArchiveEntry be sealed.
    **/
    static void RenameEntry(ZipArchiveEntry entry, string newName) {
        var nameField = typeof(ZipArchiveEntry).GetField(
            "_storedEntryName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        var bytesField = typeof(ZipArchiveEntry).GetField(
            "_storedEntryNameBytes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        nameField!.SetValue(entry, newName);
        bytesField!.SetValue(entry, Encoding.UTF8.GetBytes(newName));
    }

    public static readonly MemoryStream TestZip;

    [Fact]
    // Ensuring ZipArchive handles duplicate names still throws during normalization
    public void DuplicateEntryNames_AfterNormalization_Throws() {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) {
            archive.CreateEntry("mod/info.json");
            var dup = archive.CreateEntry("PLACEHOLDER");
            using (var s = dup.Open())
                s.Write("x"u8);
            RenameEntry(dup, "mod\\info.json");
        }
        ms.Position = 0;

        Assert.Throws<ArgumentException>(() => new BackslashTolerantZipArchive(ms));
    }

    [Fact]
    public void FullName_IsNormalized() {
        TestZip.Position = 0;
        using var archive = new BackslashTolerantZipArchive(TestZip, true);

        Assert.Contains(archive.Entries, e => e.FullName == "mod/info.json");
        Assert.DoesNotContain(archive.Entries, e => e.FullName.Contains('\\'));
    }

    [Fact]
    public void GetEntry_NormalizesArgument() {
        using var archive = new BackslashTolerantZipArchive(TestZip, true);
        Assert.NotNull(archive.GetEntry("mod/info.json"));
        Assert.NotNull(archive.GetEntry("mod\\info.json"));
    }

    [Fact]
    public void Dispose_ClosesUnderlyingStream_WhenLeaveOpenFalse() {
        var ms = new MemoryStream(TestZip.ToArray());
        var archive = new BackslashTolerantZipArchive(ms, leaveOpen: false);
        archive.Dispose();
        Assert.Throws<ObjectDisposedException>(() => ms.ReadByte());
    }

    [Fact]
    public void GetEntry_MissingEntry_ReturnsNull() {
        using var archive = new BackslashTolerantZipArchive(TestZip, leaveOpen: true);
        Assert.Null(archive.GetEntry("nonexistent/file.lua"));
    }

    [Fact]
    public void Entry_Name_IsAccessible() {
        using var archive = new BackslashTolerantZipArchive(TestZip, leaveOpen: true);
        var entry = archive.GetEntry("mod/info.json")!;
        Assert.Equal("info.json", entry.Name);
    }
}
