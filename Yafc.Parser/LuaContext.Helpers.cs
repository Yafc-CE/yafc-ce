using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json;

namespace Yafc.Parser;

internal partial class LuaContext {
    [LibraryImport(LUA)]
    [UnmanagedCallConv(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvCdecl)])]
    private static partial IntPtr lua_topointer(IntPtr state, int index);

    private void RegisterExportHelpers() {
        RegisterApi(TableToJson, "yafc_table_to_json");
        RegisterApi(EncodeString, "yafc_encode_string");
        RegisterApi(WriteFile, "yafc_write_file");
    }

    private int HelperResult(Func<string?> action) {
        int top = lua_gettop(L);
        try {
            string? result = action();
            lua_settop(L, top);
            lua_pushboolean(L, 1);
            if (result == null) {
                lua_pushnil(L);
            }
            else {
                _ = lua_pushstring(L, result);
            }
        }
        catch (Exception ex) {
            lua_settop(L, top);
            lua_pushboolean(L, 0);
            _ = lua_pushstring(L, ex.Message);
        }
        return 2;
    }

    private int TableToJson(IntPtr lua) => HelperResult(() => {
        if (lua_type(L, 1) != Type.LUA_TTABLE) {
            throw new ArgumentException("table_to_json expects a table.");
        }
        return JsonValue(1, [], 0);
    });

    // Walk the Lua stack directly: the general managed-value converter loses unsupported
    // types, and its registry-backed LuaTable wrappers are unnecessary for serialization.
    private string JsonValue(int index, HashSet<IntPtr> ancestors, int depth) {
        switch (lua_type(L, index)) {
            case Type.LUA_TBOOLEAN:
                return lua_toboolean(L, index) != 0 ? "true" : "false";
            case Type.LUA_TSTRING:
                return JsonConvert.ToString(GetString(index)!);
            case Type.LUA_TNUMBER:
                return FormatJsonNumber(lua_tonumberx(L, index, out _));
            case Type.LUA_TTABLE:
                break;
            default:
                throw new ArgumentException("Value cannot be serialized to JSON: " + lua_type(L, index));
        }

        if (depth >= 128 || lua_checkstack(L, 4) == 0) {
            throw new ArgumentException("JSON table nesting is too deep.");
        }
        IntPtr identity = lua_topointer(L, index);
        if (!ancestors.Add(identity)) {
            throw new ArgumentException("Recursive table detected.");
        }
        int top = lua_gettop(L);
        int absoluteIndex = index > 0 ? index : top + index + 1;
        var entries = new List<(string Key, double? Index, string Value)>();
        try {
            lua_pushnil(L);
            while (lua_next(L, absoluteIndex) != 0) {
                string key;
                double? numericKey = null;
                if (lua_type(L, -2) == Type.LUA_TNUMBER) {
                    numericKey = lua_tonumberx(L, -2, out _);
                    // lua_tolstring changes numbers in-place. Convert a copy so lua_next
                    // receives the original numeric key on its next iteration.
                    lua_pushvalue(L, -2);
                    key = NormalizeNumberExponent(GetString(-1)!);
                    Pop(1);
                }
                else if (lua_type(L, -2) == Type.LUA_TSTRING) {
                    key = GetString(-2)!;
                }
                else {
                    throw new ArgumentException("Values other than strings and numbers are not supported as keys");
                }
                entries.Add((key, numericKey, JsonValue(-1, ancestors, depth + 1)));
                Pop(1);
            }
        }
        finally {
            lua_settop(L, top);
            ancestors.Remove(identity);
        }

        // Only a nonempty, contiguous, one-based numeric table is a JSON array.
        if (entries.Count > 0 && entries.All(e => e.Index is double n && n >= 1 && n <= entries.Count && n == Math.Truncate(n))) {
            return "[" + string.Join(",", entries.OrderBy(e => e.Index).Select(e => e.Value)) + "]";
        }
        return "{" + string.Join(",", entries.Select(e => JsonConvert.ToString(e.Key) + ":" + e.Value)) + "}";
    }

    private static string FormatJsonNumber(double number) {
        if (double.IsNaN(number)) { return "nan"; }
        if (double.IsPositiveInfinity(number)) { return "inf"; }
        if (double.IsNegativeInfinity(number)) { return "-inf"; }

        // Factorio writes ordinary whole numbers in full, rather than using the
        // scientific notation selected by .NET's round-trip format at 1e17.
        // Factorio 2.1.24 switches powers of ten to exponent notation at 1e53.
        if (Math.Abs(number) < 1e53 && number == Math.Truncate(number)) {
            return number.ToString("F0", CultureInfo.InvariantCulture);
        }
        // Round-trip preserves short decimals. Factorio's long decimal expansions
        // are not reproduced; the regression tests document measured differences.
        return NormalizeNumberExponent(number.ToString("R", CultureInfo.InvariantCulture));
    }

    // The Windows Lua CRT uses three exponent digits (e.g. 1e+020). Preserve Lua's
    // mantissa rounding, but use Factorio's exponent spelling on every platform.
    internal static string NormalizeNumberExponent(string value) {
        int exponent = value.IndexOfAny(['e', 'E']);
        if (exponent < 0) { return value; }
        int power = int.Parse(value.AsSpan(exponent + 1), CultureInfo.InvariantCulture);
        return value[..exponent] + "e" + (power < 0 ? "-" : "+")
            + Math.Abs(power).ToString("D2", CultureInfo.InvariantCulture);
    }

    private int EncodeString(IntPtr lua) => HelperResult(() => {
        if (lua_type(L, 1) != Type.LUA_TSTRING) {
            throw new ArgumentException("encode_string expects a string.");
        }
        // Lua strings are byte sequences. Do not round-trip them through UTF-8.
        IntPtr pointer = lua_tolstring(L, 1, out nint length);
        if (length == 0) {
            throw new ArgumentException("Cannot encode an empty string.");
        }
        byte[] input = new byte[checked((int)length)];
        Marshal.Copy(pointer, input, 0, input.Length);
        using MemoryStream output = new();
        using (ZLibStream compressor = new(output, CompressionLevel.SmallestSize, leaveOpen: true)) {
            compressor.Write(input);
        }
        return Convert.ToBase64String(output.ToArray());
    });

    private bool reportedSuppressedOutput;

    private int WriteFile(IntPtr lua) => HelperResult(() => {
        if (lua_gettop(L) is < 2 or > 4) {
            throw new ArgumentException("write_file expects between 2 and 4 arguments.");
        }
        if (lua_type(L, 1) != Type.LUA_TSTRING) {
            throw new ArgumentException("write_file expects a filename string.");
        }
        if (lua_type(L, 4) is not (Type.LUA_TNONE or Type.LUA_TNIL)) {
            throw new ArgumentException("for_player cannot be used in settings and prototype stages.");
        }
        // Export-only side effects do not contribute to YAFC's prototype model. In particular,
        // never overwrite Factorio's reports while opening a planning project. Arguments have
        // already been evaluated, and execution continues after the call with no return values.
        if (!reportedSuppressedOutput) {
            logger.Information("Mod script-output writes are suppressed while loading prototypes in YAFC.");
            reportedSuppressedOutput = true;
        }
        return null;
    });
}
