using System;
using System.IO;
using Xunit;

namespace Yafc.Model.Tests;

[Collection("LuaDependentTests")]
public class ProjectTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadFromFile_CanLoadWithEmptyString(bool useMostRecent)
        => Assert.NotNull(Project.ReadFromFile("", new(), useMostRecent));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadFromFile_EmptyString_NeedsSaveButHasNoUnsavedChanges(bool useMostRecent) {
        Project project = Project.ReadFromFile("", new(), useMostRecent);

        Assert.True(project.needsSave);
        Assert.False(project.hasUnsavedChanges);
        Assert.Equal(0u, project.unsavedChangesCount);
    }

    [Fact]
    public void PerformAutoSave_NoThrowWhenLoadedWithEmptyString()
        => Project.ReadFromFile("", new(), false).PerformAutoSave();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadFromFile_CanLoadNonexistentFile(bool useMostRecent) {
        string path = CreateTestProjectPath();
        try {
            Project project = Project.ReadFromFile(path, new(), useMostRecent);

            Assert.NotNull(project);
            Assert.Equal(path, project.attachedFileName);
            Assert.True(project.needsSave);
            Assert.True(project.hasUnsavedChanges);
            Assert.Equal(1u, project.unsavedChangesCount);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_NonexistentAttachedFile_CreatesFile() {
        string path = CreateTestProjectPath();
        try {
            Project project = Project.ReadFromFile(path, new(), false);

            project.Save(path);

            Assert.True(File.Exists(path));
            Assert.False(project.needsSave);
            Assert.False(project.hasUnsavedChanges);
        }
        finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadFromFile_NewerAutosave_NeedsSaveUntilManualSave() {
        string path = CreateTestProjectPath();
        try {
            Project mainSave = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");
            mainSave.preferences.time = 1;
            mainSave.Save(path);

            mainSave.preferences.time = 60;
            string autosavePath = Project.GenerateAutosavePath(path, 1);
            using (FileStream fs = new FileStream(autosavePath, FileMode.Create, FileAccess.Write)) {
                mainSave.Save(fs);
            }

            DateTime now = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(path, now.AddMinutes(-5));
            File.SetLastWriteTimeUtc(autosavePath, now);

            Project project = Project.ReadFromFile(path, new(), true);

            Assert.Equal(path, project.attachedFileName);
            Assert.Equal(60, project.preferences.time);
            Assert.True(project.needsSave);
            Assert.True(project.hasUnsavedChanges);
            Assert.Equal(1u, project.unsavedChangesCount);

            project.Save(path);

            Assert.False(project.needsSave);
            Assert.False(project.hasUnsavedChanges);
            Assert.Equal(0u, project.unsavedChangesCount);

            Project savedProject = Project.ReadFromFile(path, new(), false);
            Assert.Equal(60, savedProject.preferences.time);
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_CleanLoadedProject_DoesNotCreateAutosave() {
        string path = CreateTestProjectPath();
        try {
            Project savedProject = new Project();
            savedProject.Save(path);

            Project project = Project.ReadFromFile(path, new(), false);

            Assert.False(project.needsSave);
            Assert.False(project.hasUnsavedChanges);

            project.PerformAutoSave();

            Assert.False(File.Exists(Project.GenerateAutosavePath(path, 1)));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_AfterManualSave_DoesNotCreateAutosave() {
        string path = CreateTestProjectPath();
        try {
            Project project = new Project();
            project.Save(path);
            project.undo.RecordChange();

            Assert.True(project.hasUnsavedChanges);

            project.Save(path);
            project.PerformAutoSave();

            Assert.False(project.needsSave);
            Assert.False(project.hasUnsavedChanges);
            Assert.False(File.Exists(Project.GenerateAutosavePath(path, 1)));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_DirtyProject_CreatesAutosave() {
        string path = CreateTestProjectPath();
        try {
            Project project = new Project();
            project.Save(path);
            project.undo.RecordChange();

            project.PerformAutoSave();

            Assert.True(File.Exists(Project.GenerateAutosavePath(path, 1)));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_MissingAttachedFile_CreatesSingleAutosaveWithoutModelEdit() {
        string path = CreateTestProjectPath();
        try {
            Project savedProject = LuaDependentTestHelper.GetProjectForLua("Yafc.Model.Tests.Model.ProductionTableContentTests.lua");
            savedProject.Save(path);

            Project project = Project.ReadFromFile(path, new(), false);
            File.Delete(path);

            Assert.True(project.hasUnsavedChanges);

            project.PerformAutoSave();
            project.PerformAutoSave();

            Assert.True(File.Exists(Project.GenerateAutosavePath(path, 1)));
            Assert.False(File.Exists(Project.GenerateAutosavePath(path, 2)));

            project.preferences.RecordUndo().time = 60;
            project.PerformAutoSave();

            Assert.True(File.Exists(Project.GenerateAutosavePath(path, 2)));
            Assert.Contains("\"time\": 60", File.ReadAllText(Project.GenerateAutosavePath(path, 2)));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }


    [Fact]
    public void Save_FlushesSuspendedUndoBatchBeforeMarkingVersionSaved() {
        string path = CreateTestProjectPath();
        try {
            Project project = new Project();
            project.Save(path);
            project.undo.Suspend();

            project.preferences.RecordUndo().time = 60;
            project.Save(path);

            Assert.Equal(0u, project.unsavedChangesCount);

            project.preferences.RecordUndo().time = 3600;

            Assert.True(project.unsavedChangesCount > 0);

            Assert.Contains("\"time\": 60", File.ReadAllText(path));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_FlushesSuspendedUndoBatchBeforeMarkingVersionAutosaved() {
        string path = CreateTestProjectPath();
        try {
            Project project = new Project();
            project.Save(path);
            project.undo.Suspend();

            project.preferences.RecordUndo().time = 60;
            project.PerformAutoSave();
            project.preferences.RecordUndo().time = 3600;
            project.PerformAutoSave();

            Assert.Contains("\"time\": 3600", File.ReadAllText(Project.GenerateAutosavePath(path, 2)));
        }
        finally {
            DeleteProjectAndAutosaves(path);
        }
    }

    [Fact]
    public void PerformAutoSave_NewUnattachedProject_DoesNotFlushSuspendedUndoBatch()
        => AssertPerformAutoSaveDoesNotFlushSuspendedUndoBatch(new Project());

    [Fact]
    public void PerformAutoSave_LoadedWithEmptyString_DoesNotFlushSuspendedUndoBatch()
        => AssertPerformAutoSaveDoesNotFlushSuspendedUndoBatch(Project.ReadFromFile("", new(), false));

    [Fact]
    public void AutosaveInDotYafcDirectory_DoesNotModifyDirectory()
        // Use Path.Combine to generate host-native path separators. (GenerateAutosavePath coincidentally converts separators.)
        => Assert.Equal(Path.Combine("home", ".yafc", "ProjectName-autosave-1.yafc"), Project.GenerateAutosavePath("home/.yafc/ProjectName.yafc", 1));

    [Fact]
    public void AutosaveInDotYafcDirectoryWithNoExtension_DoesNotModifyDirectory()
        // Use Path.Combine to generate host-native path separators. (GenerateAutosavePath coincidentally converts separators.)
        => Assert.Equal(Path.Combine("home", ".yafc", "ProjectName-autosave-4.yafc"), Project.GenerateAutosavePath("home/.yafc/ProjectName", 4));

    private static string CreateTestProjectPath() {
        string directory = Path.Combine(AppContext.BaseDirectory, "ProjectTestFiles");
        _ = Directory.CreateDirectory(directory);
        return Path.Combine(directory, Guid.NewGuid() + ".yafc");
    }

    private static void DeleteProjectAndAutosaves(string path) {
        File.Delete(path);
        for (int i = 1; i <= 5; i++) {
            File.Delete(Project.GenerateAutosavePath(path, i));
        }
    }

    private static void AssertPerformAutoSaveDoesNotFlushSuspendedUndoBatch(Project project) {
        project.undo.Suspend();

        project.preferences.RecordUndo().time = 60;

        Assert.True(project.undo.HasChangesPending(project.preferences));
        Assert.False(project.undo.CanUndo);

        project.PerformAutoSave();

        Assert.True(project.undo.HasChangesPending(project.preferences));
        Assert.False(project.undo.CanUndo);

        project.preferences.RecordUndo().time = 3600;
        project.undo.Resume();
        Assert.True(project.undo.CanUndo);
    }
}
