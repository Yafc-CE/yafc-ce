using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

internal sealed class BackslashTolerantZipEntry : IZipEntry {
    private readonly ZipArchiveEntry _entry;
    public string FullName { get; }
    public string Name { get; }
    public long Length => _entry.Length;
    public uint Crc32 => _entry.Crc32;
    public Stream Open() => _entry.Open();

    internal BackslashTolerantZipEntry(ZipArchiveEntry entry) {
        _entry = entry;
        FullName = entry.FullName.Replace('\\', '/');
        // ZipArchiveEntry.Name only splits on '\' for entries made on Windows, so take it from the normalized FullName.
        Name = FullName[(FullName.LastIndexOf('/') + 1)..];
    }
}

internal sealed class BackslashTolerantZipArchive : IZipArchive {
    readonly ZipArchive _archive;
    readonly Dictionary<string, BackslashTolerantZipEntry> _entries;

    public BackslashTolerantZipArchive(Stream stream, bool leaveOpen = false) {
        // Nobody can dispose an archive whose constructor threw, so release the stream here when we own it.
        try {
            _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);

            // Some archives list the same path more than once, either literally or after normalizing the slashes.
            // Keep the first entry, like ZipArchive.GetEntry does.
            var entries = _archive.Entries.Select(e => new BackslashTolerantZipEntry(e)).DistinctBy(e => e.FullName, StringComparer.Ordinal).ToList();
            _entries = entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
            Entries = entries.AsReadOnly();
        }
        catch {
            if (_archive != null) {
                _archive.Dispose(); // also disposes the stream, unless leaveOpen
            }
            else if (!leaveOpen) {
                stream.Dispose();
            }
            throw;
        }
    }
    public IReadOnlyCollection<IZipEntry> Entries { get; }

    public IZipEntry? GetEntry(string path) => _entries.TryGetValue(path.Replace('\\', '/'), out var entry) ? entry : null;

    public void Dispose() => _archive.Dispose();
}
