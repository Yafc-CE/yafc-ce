using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;
using Yafc.Model.Tests;

namespace Yafc.Model.Serialization.Tests;

[Collection("LuaDependentTests")]
public class ShareStringTests {
    private const string Lua = "Yafc.Model.Tests.Model.ProductionTableContentTests.lua";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecipeRow_RoundTripsToAnotherPage(bool nested) {
        (RecipeRow source, ProductionTable target) = CreateRowAndTarget(nested);
        ErrorCollector collector = new();

        RecipeRow pasted = ShareString.Decode<RecipeRow>(ShareString.Encode(source), target, collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Same(target, pasted.owner);
        Assert.Equal(GetJson(source), GetJson(pasted));

        if (nested) {
            // The links of the nested table must work without any further changes to that table.
            ProductionLink link = Assert.Single(pasted.subgroup.links);
            Assert.True(pasted.subgroup.recipes[0].FindLink(link.goods, out IProductionLink found));
            Assert.Same(link, found);
        }
    }

    [Fact]
    public void CutAndPaste_CanBeUndoneAndRedone() {
        (RecipeRow source, ProductionTable target) = CreateRowAndTarget(true);
        ProductionTable sourceTable = source.owner;
        UndoSystem undo = target.undo;
        string json = GetJson(source);
        string shareString = ShareString.Encode(source);

        sourceTable.RecordUndo().recipes.Remove(source);
        undo.FlushPendingChanges();
        RecipeRow pasted = ShareString.Decode<RecipeRow>(shareString, target, new ErrorCollector());
        target.RecordUndo().recipes.Add(pasted);
        undo.FlushPendingChanges();

        undo.PerformUndo();
        Assert.Empty(sourceTable.recipes);
        Assert.Empty(target.recipes);

        undo.PerformUndo();
        Assert.Same(source, Assert.Single(sourceTable.recipes));
        Assert.Equal(json, GetJson(source));
        Assert.Empty(target.recipes);

        undo.PerformRedo();
        undo.PerformRedo();
        Assert.Empty(sourceTable.recipes);
        Assert.Same(pasted, Assert.Single(target.recipes));
        Assert.Equal(json, GetJson(pasted));
    }

    [Fact]
    public void ProjectPage_RoundTrips() {
        (RecipeRow source, _) = CreateRowAndTarget(true);
        ProjectPage page = (ProjectPage)source.owner.owner;
        ErrorCollector collector = new();

        ProjectPage loaded = ShareString.Decode<ProjectPage>(ShareString.Encode(page), page.owner, collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(page.guid, loaded.guid);
        Assert.Equal(GetJson(source), GetJson(((ProductionTable)loaded.content).recipes.Single()));
    }

    [Fact]
    public void Header_ContainsTypeNameAndVersion() {
        (RecipeRow source, _) = CreateRowAndTarget(false);

        // Renaming these types would make the existing share strings unreadable.
        Assert.StartsWith($"YAFC\nRecipeRow\n{Project.currentYafcVersion}\n\n\n{{", Decompress(ShareString.Encode(source)));
        Assert.StartsWith($"YAFC\nProjectPage\n{Project.currentYafcVersion}\n\n\n{{", Decompress(ShareString.Encode((ProjectPage)source.owner.owner)));
    }

    [Fact]
    public void Decode_ReportsStringsItCannotLoad() {
        (RecipeRow source, ProductionTable target) = CreateRowAndTarget(false);
        string valid = ShareString.Encode(source);

        assertInvalid("");
        assertInvalid("Not a share string");
        assertInvalid(valid[..(valid.Length / 2)]);
        assertInvalid(ShareString.Encode((ProjectPage)source.owner.owner)); // a share string, but for something else

        void assertInvalid(string text) {
            ErrorCollector collector = new();
            Assert.Null(ShareString.Decode<RecipeRow>(text, target, collector));
            Assert.NotEqual(ErrorSeverity.None, collector.severity);
        }
    }

    /// <summary>
    /// Creates a marked row with a fixed product and modules (optionally, as part of a nested table) and an empty table on another page.
    /// </summary>
    private static (RecipeRow source, ProductionTable target) CreateRowAndTarget(bool nested) {
        Project project = LuaDependentTestHelper.GetProjectForLua(Lua);
        ProjectPage sourcePage = new(project, typeof(ProductionTable));
        ProjectPage targetPage = new(project, typeof(ProductionTable));
        project.pages.Add(sourcePage);
        project.pages.Add(targetPage);

        ProductionTable sourceTable = (ProductionTable)sourcePage.content;
        ProductionTable table = sourceTable;

        if (nested) {
            RecipeRow header = new(table) { description = "Nested table", tag = 2 };
            header.subgroup = new ProductionTable(header);
            table.recipes.Add(header);
            table = header.subgroup;
        }

        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow row = table.recipes.Single();
        EntityCrafter crafter = Database.allCrafters.First(c => c.moduleSlots > 0 && c.energy.fuels.Length > 1);
        IObjectWithQuality<Goods> product = row.recipe.target.products[0].goods.With(Quality.Normal);
        Module module = Database.allModules.Single(m => m.name == "speed-module");
        row.entity = crafter.With(Quality.Normal);
        row.fuel = crafter.energy.fuels[1].With(Quality.Normal);
        row.modules = new ModuleTemplateBuilder { list = { (module.With(Quality.Normal), 0) } }.Build(row);
        row.fixedBuildings = 7;
        row.fixedProduct = product;
        row.builtBuildings = 3;
        row.tag = 1;
        Assert.True(table.CreateLink(product));
        project.undo.FlushPendingChanges();

        return (sourceTable.recipes.Single(), (ProductionTable)targetPage.content);
    }

    private static string GetJson(RecipeRow row) => Encoding.UTF8.GetString(JsonUtils.SaveToJson(row).ToArray());

    private static string Decompress(string shareString) {
        using DeflateStream stream = new(new MemoryStream(Convert.FromBase64String(shareString)), CompressionMode.Decompress);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
