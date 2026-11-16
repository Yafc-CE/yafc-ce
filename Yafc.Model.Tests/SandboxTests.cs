using System.Text;
using Xunit;
using Yafc.Parser;

namespace Yafc.Model.Tests;

// The expected values come from logging the data stage environment of Factorio 2.1.
public class SandboxTests {
    private static object? Run(LuaContext context, string code) {
        context.Exec(Encoding.UTF8.GetBytes("result = " + code), "*", "test");

        return context.GetGlobal("result");
    }

    private static string KeysOf(LuaContext context, string table) => (string)Run(context, $$"""
        (function()
            local names = {}
            for name in pairs({{table}}) do names[#names + 1] = name end
            table.sort(names)
            return table.concat(names, ",")
        end)()
        """)!;

    [Fact]
    public void Sandbox_RemovesFactorioInaccessibleGlobals() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        string code = """
            (function()
                local present = {}
                for _, name in ipairs({"dofile", "getfenv", "loadfile", "setfenv", "coroutine", "module", "io", "os", "newproxy"}) do
                    if _G[name] ~= nil then present[#present + 1] = name end
                end
                return table.concat(present, ",")
            end)()
            """;

        Assert.Equal("", Run(context, code));
    }

    [Fact]
    public void Sandbox_PackageOnlyHasLoaded() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        Assert.Equal("loaded", KeysOf(context, "package"));
        Assert.Equal("", KeysOf(context, "package.loaded"));
    }

    [Fact]
    public void Sandbox_DebugOnlyHasGetinfoAndTraceback() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        Assert.Equal("getinfo,traceback", KeysOf(context, "debug"));
    }

    [Fact]
    public void Sandbox_LoadAcceptsTextChunks() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        Assert.Equal(42d, Run(context, """load("return 42")()"""));
        Assert.Equal(42d, Run(context, """load("return x", "chunk", "b", {x = 42})()"""));
        Assert.Equal(true, Run(context, "loadstring == load"));
        Assert.Equal("{a = 1}", Run(context, """serpent.line(select(2, serpent.load("{a = 1}")), {comment = false})"""));
    }

    [Fact]
    public void Sandbox_LoadRejectsBinaryChunks() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        string code = """
            (function()
                local f, err = load(string.dump(function() return 42 end), "chunk", "b")
                return f == nil and err:find("binary") ~= nil
            end)()
            """;

        Assert.Equal(true, Run(context, code));
    }

    [Fact]
    public void Sandbox_GetinfoHidesLoadShim() {
        using LuaContext context = LuaDependentTestHelper.CreateSandboxedContext();

        Assert.Equal("C", Run(context, "debug.getinfo(load).what"));
    }
}
