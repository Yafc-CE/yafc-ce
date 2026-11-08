using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

internal sealed class ZipArchiveAdapter : IZipArchive {
    readonly ZipArchive _archive;

    public ZipArchiveAdapter(Stream stream, bool leaveOpen = false) {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        // Keep the first of duplicate entries, like ZipArchive.GetEntry and BackslashTolerantZipArchive do.
        Entries = _archive.Entries.DistinctBy(e => e.FullName, StringComparer.Ordinal).Select(e => (IZipEntry)new ZipEntryAdapter(e)).ToList().AsReadOnly();
    }

    public IReadOnlyCollection<IZipEntry> Entries { get; }

    public IZipEntry? GetEntry(string path) => _archive.GetEntry(path) is { } e ? new ZipEntryAdapter(e) : null;
    public void Dispose() => _archive.Dispose();
}

internal sealed class ZipEntryAdapter(ZipArchiveEntry entry) : IZipEntry {
    public string FullName => entry.FullName;
    public string Name => entry.Name;
    public long Length => entry.Length;
    public uint Crc32 => entry.Crc32;
    public Stream Open() => entry.Open();
}
