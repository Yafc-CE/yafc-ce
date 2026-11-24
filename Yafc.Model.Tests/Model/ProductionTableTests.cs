using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Yafc.I18n;

namespace Yafc.Model.Tests.Model;

[Collection("LuaDependentTests")]
public class ProductionTableTests {
    private const string InfeasibleLinkLua = "Yafc.Model.Tests.Model.ProductionTableTests.InfeasibleLink.lua";

    [Fact]
    public void CreateLink_WithSuspendedImmediateScheduler_UpdatesLinkMapAfterMutation() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");
        Project project = new();
        ProjectPage page = new(project, typeof(ProductionTable));
        ProductionTable table = (ProductionTable)page.content;
        Goods goodsTarget = Database.items.all.First(g => g.isLinkable);
        IObjectWithQuality<Goods> goods = goodsTarget.With(Quality.Normal);
        project.undo.Suspend();

        Assert.True(table.CreateLink(goods));

        ProductionLink link = Assert.Single(table.links);
        Assert.Same(link, table.linkMap[link.goods]);
        Assert.False(table.CreateLink(goods));
        Assert.Single(table.links);
    }

    [Fact]
    public void DestroyLink_WithSuspendedImmediateScheduler_UpdatesLinkMapAfterMutation() {
        _ = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");
        Project project = new();
        ProjectPage page = new(project, typeof(ProductionTable));
        ProductionTable table = (ProductionTable)page.content;
        Goods goodsTarget = Database.items.all.First(g => g.isLinkable);
        IObjectWithQuality<Goods> goods = goodsTarget.With(Quality.Normal);
        project.undo.Suspend();
        Assert.True(table.CreateLink(goods));
        ProductionLink link = Assert.Single(table.links);

        Assert.True(link.Destroy());

        Assert.Empty(table.links);
        Assert.False(table.linkMap.ContainsKey(goods));
        Assert.True(table.CreateLink(goods));
        Assert.Single(table.links);
    }

    [Fact]
    public async Task Solve_UsesProjectThreadSwitcher_ForInfeasibleLink() {
        Project project = LuaDependentTestHelper.GetProjectForLua(InfeasibleLinkLua);
        CountingModelThreadSwitcher switcher = new();
        project.modelThreadSwitcher = switcher;

        RecipeOrTechnology producerRecipe = Database.recipes.all.Single(r => r.name == "recipe");
        RecipeOrTechnology consumerRecipe = Database.recipes.all.Single(r => r.name == "recipe2");
        Goods dummy3 = Database.items.all.Single(g => g.name == "dummy_3");
        Goods ash = Database.items.all.Single(g => g.name == "ash");
        IObjectWithQuality<Goods> dummy3Link = dummy3.With(Quality.Normal);
        IObjectWithQuality<Goods> ashLink = ash.With(Quality.Normal);
        CostAnalysis.Instance.cost[dummy3] = 1f;
        CostAnalysis.Instance.cost[ash] = 1f;

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;

        table.AddRecipe(producerRecipe.With(Quality.Normal), DataUtils.DeterministicComparer);
        table.AddRecipe(consumerRecipe.With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow producer = table.recipes[0];
        RecipeRow consumer = table.recipes[1];

        producer.fixedBuildings = 1f;
        consumer.fixedBuildings = 1f;
        Assert.True(table.CreateLink(dummy3Link));
        Assert.True(table.CreateLink(ashLink));
        ProductionLink dummy3LinkRecord = Assert.Single(table.links, link => link.goods == dummy3Link);
        ProductionLink ashLinkRecord = Assert.Single(table.links, link => link.goods == ashLink);
        dummy3LinkRecord.amount = 1f;
        ashLinkRecord.amount = 1f;

        Recipe recoveryRecipe = Assert.IsType<Recipe>(producerRecipe);
        table.AddRecipe(recoveryRecipe.With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow recoveryRow = table.recipes[2];
        recoveryRow.fixedBuildings = 1f;
        IObjectWithQuality<Goods> recoveryGoods = Database.goods.all.Single(g => g.name == "dummy_1").With(Quality.Normal);
        Assert.True(table.CreateLink(recoveryGoods));
        ProductionLink recoveryLink = Assert.Single(table.links, link => link.goods == recoveryGoods);
        recoveryLink.amount = 1f;

        var error = await table.Solve(page);

        Assert.NotNull(error);
        Assert.Equal(1, switcher.backgroundSwitches);
        // ProductionTable.Solve is called directly here, so ProjectPage.ExternalSolve's
        // foreground re-entry is not counted. Today Solve switches back to foreground
        // only inside the infeasibility diagnostics branch exercised by this test.
        Assert.Equal(1, switcher.foregroundSwitches);
        Assert.Equal([CountingModelThreadSwitcher.SwitchEvent.Background, CountingModelThreadSwitcher.SwitchEvent.Foreground], switcher.events);
    }

    [Fact]
    public async Task Solve_ReportsCostsIgnoredBeforeBuiltCount_WhenOverproductionIsRequired() {
        Project project = LuaDependentTestHelper.GetProjectForLua(InfeasibleLinkLua);

        Goods dummy3 = Database.items.all.Single(g => g.name == "dummy_3");
        Goods ash = Database.items.all.Single(g => g.name == "ash");
        CostAnalysis.Instance.cost[dummy3] = 1f;
        CostAnalysis.Instance.cost[ash] = 1f;

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe2").With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow producer = table.recipes[0];
        RecipeRow ashConsumer = table.recipes[1];

        // Exporting dummy_3 forces the producer to make more ash than the fixed consumer can use,
        // so the ash link can only be satisfied with overproduction (slack).
        Assert.True(table.CreateLink(dummy3.With(Quality.Normal)));
        Assert.True(table.CreateLink(ash.With(Quality.Normal)));
        table.links.Single(l => l.goods.target == dummy3).amount = 10f;
        ashConsumer.fixedBuildings = 0.01f;
        producer.builtBuildings = 0;

        var error = await table.Solve(page);

        Assert.True(producer.parameters.warningFlags.HasFlag(WarningFlags.OverproductionRequired));
        Assert.True(producer.parameters.warningFlags.HasFlag(WarningFlags.ExceedsBuiltCount));
        Assert.Equal(LSs.ProductionTableCostsIgnored.L(), error);
    }

    [Fact]
    public void ContainsAnywhere_AfterMovingNestedTableToAnotherPage_SearchesNewPage() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");
        ProductionTable source = (ProductionTable)new ProjectPage(project, typeof(ProductionTable)).content;
        ProductionTable target = (ProductionTable)new ProjectPage(project, typeof(ProductionTable)).content;
        RecipeRow header = new(source);
        header.subgroup = new ProductionTable(header);
        source.recipes.Add(header);
        Recipe recipe = Database.recipes.all.Single(r => r.name == "recipe");
        target.AddRecipe(recipe.With(Quality.Normal), DataUtils.DeterministicComparer);
        Assert.False(header.subgroup.ContainsAnywhere(recipe));

        source.RecordUndo().recipes.Remove(header);
        header.SetOwner(target);
        target.RecordUndo().recipes.Add(header);

        Assert.True(header.subgroup.ContainsAnywhere(recipe));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithRecipe() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithEmptySubtable() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanSaveAndLoadWithNonemptySubtable() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);
        row.subgroup.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Fact]
    public void ProductionTableTest_CanLoadWithUnexpectedObject() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        table.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);
        RecipeRow row = table.GetAllRecipes().Single();
        row.subgroup = new ProductionTable(row);

        // Force the subgroup to have a value in modules, which is not present in a normal project.
        typeof(ProductionTable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single(f => f.FieldType == typeof(ModuleFillerParameters))
            .SetValue(row.subgroup, new ModuleFillerParameters(row.subgroup));

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector); // This reader is expected to skip the unexpected value with a MinorDataLoss warning.

        Assert.Equal(ErrorSeverity.MinorDataLoss, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    [Theory]
    [MemberData(nameof(ProjectPageContentTypes))]
    public void ProductionTableTest_CanSaveAndLoadWithEachPageContentType(Type contentType) {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, contentType);
        project.pages.Add(page);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
    }

    public static TheoryData<Type> ProjectPageContentTypes => [.. AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes())
        .Where(t => typeof(ProjectPageContents).IsAssignableFrom(t) && !t.IsAbstract)];

    [Fact]
    public void ProductionTableTest_CanLoadWithNullRecipe() {
        Project project = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");

        ProjectPage page = new(project, typeof(ProductionTable));
        project.pages.Add(page);
        ProductionTable table = (ProductionTable)page.content;
        RecipeRow nullRecipe = new RecipeRow(table, null);
        table.recipes.Add(nullRecipe);
        nullRecipe.subgroup = new ProductionTable(nullRecipe);
        nullRecipe.subgroup.AddRecipe(Database.recipes.all.Single(r => r.name == "recipe").With(Quality.Normal), DataUtils.DeterministicComparer);

        ErrorCollector collector = new();
        using MemoryStream stream = new();
        project.Save(stream);
        Project newProject = Project.Read(stream.ToArray(), collector);

        Assert.Equal(ErrorSeverity.None, collector.severity);
        Assert.Equal(project.pages.Select(s => s.guid), newProject.pages.Select(p => p.guid));
        Assert.Equal(((ProductionTable)project.pages[0].content).GetAllRecipes().Select(r => r.recipe),
            ((ProductionTable)newProject.pages[0].content).GetAllRecipes().Select(r => r.recipe));
    }
}
