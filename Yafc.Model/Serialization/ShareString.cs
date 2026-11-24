using System;
using System.Buffers.Text;
using System.IO;
using System.IO.Compression;
using System.Text;
using Yafc.I18n;

namespace Yafc.Model;

/// <summary>
/// Converts model objects to and from share strings: text that can be put on the clipboard, and loaded into the same or another project.
/// </summary>
/// <remarks>A share string is the base64 encoding of compressed data. The data starts with a header of five lines: "YAFC", the type name of the
/// shared object, the version of YAFC that created the string, and two reserved (empty) lines. The header is followed by the JSON of the object.</remarks>
public static class ShareString {
    /// <summary>
    /// Create a share string that contains <paramref name="obj"/>.
    /// </summary>
    public static string Encode<T>(T obj) where T : ModelObject {
        using MemoryStream data = JsonUtils.SaveToJson(obj);
        using MemoryStream targetStream = new MemoryStream();
        using (DeflateStream compress = new DeflateStream(targetStream, CompressionLevel.Optimal, true)) {
            // write some magic chars and version as a marker
            compress.Write(Encoding.UTF8.GetBytes($"YAFC\n{typeof(T).Name}\n{Project.currentYafcVersion}\n\n\n"));
            data.CopyTo(compress);
        }

        return Convert.ToBase64String(targetStream.GetBuffer(), 0, (int)targetStream.Length);
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="text"/> is a share string that contains a <typeparamref name="T"/>. This decodes the text, but
    /// only decompresses the start of the header, so <see cref="Decode"/> can still fail.
    /// </summary>
    public static bool Contains<T>(string text) where T : ModelObject {
        if (!Base64.IsValid(text)) {
            // Most text is rejected here, at its first invalid character, before allocating anything.
            return false;
        }

        try {
            using DeflateStream stream = new DeflateStream(new MemoryStream(Convert.FromBase64String(text)), CompressionMode.Decompress);
            byte[] expected = Encoding.UTF8.GetBytes($"YAFC\n{typeof(T).Name}\n");
            byte[] actual = new byte[expected.Length];

            return stream.ReadAtLeast(actual, actual.Length, false) == actual.Length && actual.AsSpan().SequenceEqual(expected);
        }
        catch (InvalidDataException) {
            return false;
        }
    }

    /// <summary>
    /// Load the object from a share string. It is not added to <paramref name="owner"/>.
    /// </summary>
    /// <param name="text">The text to load, which does not need to be a valid share string.</param>
    /// <param name="owner">The owner of the loaded object.</param>
    /// <param name="collector">Receives all problems that occurred while loading the object.</param>
    /// <returns>The loaded object, or <see langword="null"/> if <paramref name="text"/> is not a share string that contains a <typeparamref name="T"/>.</returns>
    public static T? Decode<T>(string text, ModelObject owner, ErrorCollector collector) where T : ModelObject {
        try {
            byte[] compressedBytes = Convert.FromBase64String(text.Trim());
            using DeflateStream deflateStream = new DeflateStream(new MemoryStream(compressedBytes), CompressionMode.Decompress);
            using MemoryStream ms = new MemoryStream();
            deflateStream.CopyTo(ms);
            byte[] bytes = ms.ToArray();
            int index = 0;

#pragma warning disable IDE0078 // Use pattern matching: False positive detection that changes code behavior
            if (DataUtils.ReadLine(bytes, ref index) != "YAFC" || DataUtils.ReadLine(bytes, ref index) != typeof(T).Name) {
#pragma warning restore IDE0078
                throw new InvalidDataException();
            }

            Version version = new Version(DataUtils.ReadLine(bytes, ref index) ?? "");
            if (version > Project.currentYafcVersion) {
                collector.Error(LSs.AlertImportPageNewerVersion.L(version), ErrorSeverity.Important);
            }

            _ = DataUtils.ReadLine(bytes, ref index); // reserved 1
            if (DataUtils.ReadLine(bytes, ref index) != "") { // reserved 2 but this time it is required to be empty
                throw new NotSupportedException(LSs.AlertImportPageIncompatibleVersion.L(version));
            }

            return JsonUtils.LoadFromJson<T>(bytes.AsSpan(index), owner, collector);
        }
        catch (Exception ex) {
            collector.Exception(ex, LSs.AlertImportPageInvalidString, ErrorSeverity.Critical);
            return null;
        }
    }
}
