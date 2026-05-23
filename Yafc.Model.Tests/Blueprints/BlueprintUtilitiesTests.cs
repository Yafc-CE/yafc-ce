using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;
using Yafc.Blueprints;

namespace Yafc.Model.Tests.Blueprints;

public class BlueprintUtilitiesTests {
    [Fact]
    public void ExportConstantCombinators_WithEmptyGoods_ReturnsEmptyBlueprint() {
        string blueprintString = BlueprintUtilities.ExportConstantCombinators(
            "empty",
            Array.Empty<(IObjectWithQuality<Goods> item, int amount)>());

        AssertEmptyBlueprint(blueprintString, "empty");
    }

    [Fact]
    public void ExportRequesterChests_WithEmptyGoods_ReturnsEmptyBlueprint() {
        EntityContainer chest = new() {
            logisticSlotsCount = 10,
        };

        string blueprintString = BlueprintUtilities.ExportRequesterChests(
            "empty",
            Array.Empty<(IObjectWithQuality<Item> item, int amount)>(),
            chest);

        AssertEmptyBlueprint(blueprintString, "empty");
    }

    [Fact]
    public void ExportConstantCombinators_WithEmptyGoodsAndRawJson_ReturnsEmptyBlueprint() {
        string blueprintString = BlueprintUtilities.ExportConstantCombinators(
            "empty",
            Array.Empty<(IObjectWithQuality<Goods> item, int amount)>(),
            BlueprintFormat.RawJson);

        AssertEmptyBlueprintJson(blueprintString, "empty");
    }

    [Fact]
    public void ExportRequesterChests_WithEmptyGoodsAndRawJson_ReturnsEmptyBlueprint() {
        EntityContainer chest = new() {
            logisticSlotsCount = 10,
        };

        string blueprintString = BlueprintUtilities.ExportRequesterChests(
            "empty",
            Array.Empty<(IObjectWithQuality<Item> item, int amount)>(),
            chest,
            BlueprintFormat.RawJson);

        AssertEmptyBlueprintJson(blueprintString, "empty");
    }

    private static void AssertEmptyBlueprint(string blueprintString, string name) {
        Assert.StartsWith("0", blueprintString);
        AssertEmptyBlueprintJson(DecodeBlueprintString(blueprintString), name);
    }

    private static void AssertEmptyBlueprintJson(string blueprintJson, string name) {
        using JsonDocument document = JsonDocument.Parse(blueprintJson);
        JsonElement blueprint = document.RootElement.GetProperty("blueprint");

        Assert.Equal("blueprint", blueprint.GetProperty("item").GetString());
        Assert.Equal(name, blueprint.GetProperty("label").GetString());
        Assert.Empty(blueprint.GetProperty("entities").EnumerateArray());
    }

    private static string DecodeBlueprintString(string blueprintString) {
        byte[] compressed = Convert.FromBase64String(blueprintString[1..]);
        using MemoryStream input = new MemoryStream(compressed);
        using ZLibStream zlib = new ZLibStream(input, CompressionMode.Decompress);
        using MemoryStream output = new MemoryStream();
        zlib.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
