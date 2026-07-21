using System;
using System.Collections.Generic;
using System.IO;

internal interface IZipArchive : IDisposable {
    IReadOnlyCollection<IZipEntry> Entries { get; }
    IZipEntry? GetEntry(string path);
}

internal interface IZipEntry {
    string FullName { get; }
    string Name { get; }
    long Length { get; }
    uint Crc32 { get; }
    Stream Open();

}
