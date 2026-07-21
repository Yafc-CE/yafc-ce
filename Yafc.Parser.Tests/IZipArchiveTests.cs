namespace Yafc.Parser.Tests;

public class IZipArchiveTests {
    public static IEnumerable<object[]> Archives() {
        yield return [new BackslashTolerantZipArchive(TestZip, leaveOpen: true)];
        yield return [new ZipArchiveAdapter(TestZip, leaveOpen: true)];
    }

    private static MemoryStream TestZip => BackslashTolerantZipArchiveTests.TestZip;

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
