using System.IO.Compression;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Yafc.Parser.Tests;

public class LuaExportHelpersTests {
    private static LuaContext CreateContext() {
        LuaContext context = new(new Version(2, 1, 24));
        // The export tests need the real sandbox wrappers, but no Factorio install.
        _ = context.Exec("require = function() return {} end"u8, "test", "setup");
        int defines = context.Exec("return {prototypes={}}"u8, "test", "defines");
        _ = context.Exec(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Data", "Sandbox.lua")), "test", "sandbox", defines);
        return context;
    }

    [Theory]
    [InlineData("1e+020", "1e+20")]
    [InlineData("1e+015", "1e+15")]
    [InlineData("1e-007", "1e-07")]
    [InlineData("1e+100", "1e+100")]
    [InlineData("1E+20", "1e+20")]
    [InlineData("0.33333333333333", "0.33333333333333")]
    public void NormalizesWindowsLuaExponents(string input, string expected)
        => Assert.Equal(expected, LuaContext.NormalizeNumberExponent(input));

    [Fact]
    public void ExportWrappersReportNativeDebugInfo() {
        using LuaContext context = CreateContext();
        _ = context.Exec("""
            for _, name in ipairs({'table_to_json', 'encode_string', 'write_file'}) do
                local info = debug.getinfo(helpers[name], 'Su')
                assert(info.what == 'C', name)
                assert(info.source == '=[C]', name)
                assert(info.nups == 0, name)
            end
            """u8, "test", "native-helper-info");
    }

    // Expected values measured with Factorio 2.1.24 in an isolated prototype-stage mod.
    [Theory]
    [InlineData("{}", "{}")]
    [InlineData("{3,2,1}", "[3,2,1]")]
    [InlineData("{[1]='a',[3]='c'}", "{\"1\":\"a\",\"3\":\"c\"}")]
    [InlineData("{[1]='a',foo='b'}", "{\"1\":\"a\",\"foo\":\"b\"}")]
    [InlineData("{[0]='a',[1]='b'}", "{\"1\":\"b\",\"0\":\"a\"}")]
    [InlineData("{text='snow 雪\\n\\0',nested={true,false}}", "{\"text\":\"snow 雪\\n\\u0000\",\"nested\":[true,false]}")]
    [InlineData("{1e20}", "[100000000000000000000]")]
    [InlineData("{1e22}", "[10000000000000000000000]")]
    [InlineData("{-1e22}", "[-10000000000000000000000]")]
    [InlineData("{1e16,1e17}", "[10000000000000000,100000000000000000]")]
    [InlineData("{1e53}", "[1e+53]")]
    [InlineData("{1e300}", "[1e+300]")]
    [InlineData("{1e-6,1e-5,-0.0}", "[1e-06,1e-05,-0]")]
    [InlineData("{1e-7}", "[1e-07]")]
    [InlineData("{0.1,1.5,-2.5,0.125}", "[0.1,1.5,-2.5,0.125]")]
    [InlineData("{[1/3]='x'}", "{\"0.33333333333333\":\"x\"}")]
    [InlineData("{[1e15]='x'}", "{\"1e+15\":\"x\"}")]
    [InlineData("{[1e20]='x'}", "{\"1e+20\":\"x\"}")]
    [InlineData("{[0.1]='x'}", "{\"0.1\":\"x\"}")]
    public void SerializesTables(string input, string expected) {
        using LuaContext context = CreateContext();
        _ = context.Exec(Encoding.UTF8.GetBytes("result = helpers.table_to_json(" + input + ")"), "test", "json");
        string actual = (string)context.GetGlobal("result")!;
        Assert.True(JToken.DeepEquals(JToken.Parse(expected), JToken.Parse(actual)), $"Expected {expected}; actual {actual}");
        // Compare numeric strings exactly: semantic JSON comparisons hide formatting bugs.
        if (expected.StartsWith('[') || JToken.Parse(expected) is JObject { Count: 1 }) {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void RejectsUnsupportedValuesWithoutCorruptingLuaState() {
        using LuaContext context = CreateContext();
        _ = context.Exec("""
            local cycle = {}; cycle.x = cycle
            for _, value in ipairs({cycle, {x=function() end}, {[true]='a'}, 'scalar'}) do
                assert(not pcall(helpers.table_to_json, value))
                assert(helpers.table_to_json({42}) == '[42]')
            end
            local shared = {x=1}
            assert(helpers.table_to_json({shared,shared}) == '[{"x":1},{"x":1}]')
            assert(helpers.table_to_json({0/0,1/0,-1/0}) == '[nan,inf,-inf]')
            """u8, "test", "errors");
    }

    [Fact]
    public void EncodingPreservesBinaryLuaStringsAndUsesFactorioZlibFormat() {
        using LuaContext context = CreateContext();
        _ = context.Exec("encoded = helpers.encode_string('hello\\0world'); binary = helpers.encode_string(string.char(0,255,128,65)); assert(not pcall(helpers.encode_string, 123))"u8, "test", "encode");
        // Factorio's output for hello\0world is eNrLSM3JyWcozy/KSQEAGUsEPQ==.
        static byte[] Inflate(string encoded) {
            using MemoryStream source = new(Convert.FromBase64String(encoded));
            using ZLibStream zlib = new(source, CompressionMode.Decompress);
            using MemoryStream result = new();
            zlib.CopyTo(result);
            return result.ToArray();
        }
        Assert.Equal(Inflate("eNrLSM3JyWcozy/KSQEAGUsEPQ=="), Inflate((string)context.GetGlobal("encoded")!));
        Assert.Equal(new byte[] { 0, 255, 128, 65 }, Inflate((string)context.GetGlobal("binary")!));
    }

    [Theory]
    [InlineData("123.456", "123.456", "123.4560000000000030695446184836328029632568359375")]
    [InlineData("1/3", "0.3333333333333333", "0.333333333333333303727386009995825588703155517578125")]
    [InlineData("2^63", "9223372036854775808", "9223372036854776028")]
    [InlineData("1e23", "99999999999999991611392", "109909909909909904206802")]
    [InlineData("1e24", "999999999999999983222784", "1000000000000000000000024")]
    public void DocumentsKnownFactorioNumberDifferences(string input, string expected, string factorio) {
        using LuaContext context = CreateContext();
        _ = context.Exec(Encoding.UTF8.GetBytes("result = helpers.table_to_json({" + input + "})"), "test", "known-difference");
        Assert.Equal("[" + expected + "]", context.GetGlobal("result"));
        Assert.NotEqual("[" + factorio + "]", context.GetGlobal("result"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ManagedExceptionsReturnFailureInsteadOfEscapingCallback(bool overflow) {
        using LuaContext context = CreateContext();
        // Inject failures at the callback boundary, including an exception type outside the
        // original filter. Pop the native results to verify stack recovery before more Lua runs.
        MethodInfo helper = typeof(LuaContext).GetMethod("HelperResult", BindingFlags.Instance | BindingFlags.NonPublic)!;
        MethodInfo pop = typeof(LuaContext).GetMethod("PopManagedValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Func<string?> action = () => throw (overflow ? new OverflowException("test failure") : new IOException("test failure"));
        Assert.Equal(2, helper.Invoke(context, [action]));
        Assert.Equal("test failure", pop.Invoke(context, [1]));
        Assert.Equal(false, pop.Invoke(context, [1]));
        _ = context.Exec("assert(helpers.table_to_json({1}) == '[1]')"u8, "test", "after-failure");
    }

    [Fact]
    public void EmptyEncodingRaisesCatchableErrorAndPreservesState() {
        using LuaContext context = CreateContext();
        _ = context.Exec("""
            local ok, message = pcall(helpers.encode_string, '')
            assert(not ok)
            assert(message:find('Cannot encode an empty string.', 1, true))
            assert(type(helpers.encode_string('still working')) == 'string')
            """u8, "test", "empty-encoding");
    }

    [Fact]
    public void ExportDoesNotWriteFilesOrSkipSubsequentPrototypeChanges() {
        using LuaContext context = CreateContext();
        string path = Path.Combine(Path.GetTempPath(), "yafc-helper-" + Guid.NewGuid() + ".txt");
        context.SetGlobal("test_path", path);
        _ = context.Exec("""
            data = {raw={technology={test={unit={count=10}}}}}
            local tech = data.raw.technology.test
            tech.unit.count = tech.unit.count * 3
            local export = helpers.encode_string(helpers.table_to_json({count=tech.unit.count}))
            assert(select('#', helpers.write_file(test_path, export)) == 0)
            helpers.write_file(test_path, 'append', true)
            assert(not pcall(helpers.write_file, test_path, 'x', false, 0))
            assert(not pcall(helpers.write_file, test_path))
            assert(not pcall(helpers.write_file, {}, 'x'))
            assert(not pcall(helpers.write_file, 123, 'x'))
            tech.unit.count = tech.unit.count + 7
            result = tech.unit.count
            """u8, "test", "export");
        Assert.Equal(37d, context.GetGlobal("result"));
        Assert.False(File.Exists(path));
    }
}
