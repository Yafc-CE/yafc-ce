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

    // A zip that lists a folder and a file twice, with different contents for the two copies of the file.
    internal static MemoryStream CreateDuplicateEntriesZip() {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) {
            archive.CreateEntry("mod/graphics/");
            using (var s = archive.CreateEntry("mod/info.json").Open())
                s.Write("first"u8);
            archive.CreateEntry("mod/graphics/");
            using (var s = archive.CreateEntry("mod/info.json").Open())
                s.Write("second"u8);
        }
        ms.Position = 0;
        return ms;
    }

    internal static string ReadEntry(IZipEntry entry) {
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    // ZipArchiveEntry.Name splits on '\' only for entries made on Windows, so mark the entry as made on Unix (3 in the zip spec).
    static void MarkMadeOnUnix(ZipArchiveEntry entry) {
        var platformField = typeof(ZipArchiveEntry).GetField(
            "_versionMadeByPlatform",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        platformField!.SetValue(entry, Enum.ToObject(platformField.FieldType, 3));
    }

    public static readonly MemoryStream TestZip;

    [Fact]
    public void Name_IsNormalized_ForBackslashEntryMadeOnUnix() {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) {
            var entry = archive.CreateEntry("PLACEHOLDER");
            RenameEntry(entry, "mod\\info.json");
            MarkMadeOnUnix(entry);
        }
        ms.Position = 0;

        using var zip = new BackslashTolerantZipArchive(ms);

        Assert.Equal("info.json", zip.GetEntry("mod/info.json")!.Name);
    }

    [Fact]
    public void Constructor_InvalidZip_DisposesStream() {
        var ms = new MemoryStream("not a zip"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => new BackslashTolerantZipArchive(ms));
        Assert.Throws<ObjectDisposedException>(() => ms.ReadByte());
    }

    [Fact]
    public void Constructor_InvalidZip_LeaveOpen_KeepsStreamOpen() {
        var ms = new MemoryStream("not a zip"u8.ToArray());

        Assert.Throws<InvalidDataException>(() => new BackslashTolerantZipArchive(ms, leaveOpen: true));
        ms.Position = 0; // throws if the stream was disposed
        Assert.Equal('n', ms.ReadByte());
    }

    [Fact]
    public void DuplicateEntryNames_AfterNormalization_KeepsFirstEntry() {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) {
            using (var s = archive.CreateEntry("mod/info.json").Open())
                s.Write("first"u8);
            var dup = archive.CreateEntry("PLACEHOLDER");
            using (var s = dup.Open())
                s.Write("second"u8);
            RenameEntry(dup, "mod\\info.json");
        }
        ms.Position = 0;

        using var zip = new BackslashTolerantZipArchive(ms);

        Assert.Single(zip.Entries);
        Assert.Equal("first", ReadEntry(zip.GetEntry("mod/info.json")!));
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
