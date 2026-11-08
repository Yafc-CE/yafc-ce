namespace Yafc.Parser.Tests;

public class IZipArchiveTests {
    public static IEnumerable<object[]> Archives() {
        yield return [new BackslashTolerantZipArchive(TestZip, leaveOpen: true)];
        yield return [new ZipArchiveAdapter(TestZip, leaveOpen: true)];
    }

    public static IEnumerable<object[]> DuplicateEntriesArchives() {
        yield return [new BackslashTolerantZipArchive(BackslashTolerantZipArchiveTests.CreateDuplicateEntriesZip())];
        yield return [new ZipArchiveAdapter(BackslashTolerantZipArchiveTests.CreateDuplicateEntriesZip())];
    }

    private static MemoryStream TestZip => BackslashTolerantZipArchiveTests.TestZip;

    [Theory]
    [MemberData(nameof(DuplicateEntriesArchives))]
    internal void DuplicateEntryNames_KeepsFirstEntry(IZipArchive archive) {
        using (archive) {
            Assert.Equal(2, archive.Entries.Count);
            Assert.Single(archive.Entries, e => e.FullName == "mod/graphics/");
            Assert.Equal("first", BackslashTolerantZipArchiveTests.ReadEntry(archive.GetEntry("mod/info.json")!));
        }
    }

    [Theory]
    [MemberData(nameof(Archives))]
    internal void GetEntry_FindsExistingEntry(IZipArchive archive) {
        Assert.NotNull(archive.GetEntry("mod/info.json"));
    }

    [Theory]
    [MemberData(nameof(Archives))]
    internal void FullName_IsAccessible(IZipArchive archive) {
        var entry = archive.GetEntry("mod/info.json")!;
        Assert.Contains("info.json", entry.FullName);
    }
}
