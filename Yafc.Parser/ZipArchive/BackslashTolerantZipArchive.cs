using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

internal sealed class BackslashTolerantZipEntry : IZipEntry {
    private readonly ZipArchiveEntry _entry;
    public string FullName { get; }
    public string Name => _entry.Name;
    public long Length => _entry.Length;
    public uint Crc32 => _entry.Crc32;
    public Stream Open() => _entry.Open();

    internal BackslashTolerantZipEntry(ZipArchiveEntry entry) {
        _entry = entry;
        FullName = entry.FullName.Replace('\\', '/');
    }
}

internal sealed class BackslashTolerantZipArchive : IZipArchive {
    readonly ZipArchive _archive;
    readonly Dictionary<string, BackslashTolerantZipEntry> _entries;

    public BackslashTolerantZipArchive(Stream stream, bool leaveOpen = false) {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);

        // Some archives list the same path more than once, either literally or after normalizing the slashes.
        // Keep the first entry, like ZipArchive.GetEntry does.
        var entries = _archive.Entries.Select(e => new BackslashTolerantZipEntry(e)).DistinctBy(e => e.FullName, StringComparer.Ordinal).ToList();
        _entries = entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
        Entries = entries.AsReadOnly();
    }
    public IReadOnlyCollection<IZipEntry> Entries { get; }

    public IZipEntry? GetEntry(string path) => _entries.TryGetValue(path.Replace('\\', '/'), out var entry) ? entry : null;

    public void Dispose() => _archive.Dispose();
}
