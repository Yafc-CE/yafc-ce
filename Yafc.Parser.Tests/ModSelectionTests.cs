using System.IO.Compression;
using System.Text;
using static Yafc.Parser.FactorioDataSource;

namespace Yafc.Parser.Tests;

public class ModSelectionTests {
    [Theory]
    [InlineData("2.1.1", "2.0.5", "2.0.5")]
    [InlineData("2.0.5", "2.1.1", "2.0.5")]
    [InlineData("2.1.1", "2.0.5", null)]
    [InlineData("2.0.5", "2.1.1", null)]
    [InlineData("2.1.1", "2.0.5", "1.0.0")]
    public void SelectsRequestedVersionRegardlessOfDiscoveryOrder(string first, string second, string? requested) {
        using ModInfo firstMod = CreateMod(first);
        using ModInfo secondMod = CreateMod(second);
        Version? requestedVersion = requested == null ? null : new(requested);
        ModInfo? selected = null;
        foreach (ModInfo candidate in new[] { firstMod, secondMod }) {
            if (ShouldReplaceMod(selected, candidate, requestedVersion)) {
                selected = candidate;
            }
        }
        Assert.Equal(requested == "2.0.5" ? "2.0.5" : "2.1.1", selected?.version);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrefersFolderForSameRequestedVersion(bool archiveFirst) {
        using var archive = new ZipArchive(new MemoryStream(), ZipArchiveMode.Create);
        using ModInfo zipped = CreateMod("2.0.5", archive);
        using ModInfo folder = CreateMod("2.0.5");
        ModInfo first = archiveFirst ? zipped : folder;
        ModInfo second = archiveFirst ? folder : zipped;
        ModInfo selected = ShouldReplaceMod(first, second, new Version("2.0.5")) ? second : first;
        Assert.Same(folder, selected);
    }

    private static ModInfo CreateMod(string version, ZipArchive? archive = null) =>
        new("test-mod/", Encoding.UTF8.GetBytes($$"""{"name":"test-mod","version":"{{version}}","factorio_version":"2.0"}"""), 0, 0, archive);
}
