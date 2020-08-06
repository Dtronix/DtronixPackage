using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests
{
    public class PackageBuilder
    {
        private readonly string _basePath;

        public PackageBuilder(string basePath)
        {
            _basePath = basePath;
        }

        public async Task<string> CreateFile((string Path, object Data)[] contents)
        {
            var path = Path.Combine(_basePath, Guid.NewGuid() + ".file");
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var archive = await CreateZipArchive(fileStream, contents);

            return path;
        }

        public static Task<ZipArchive> CreateZipArchive((string Path, object Data)[] contents)
        {
            var ms = new MemoryStream();
            var archive = CreateZipArchive(ms, contents);
            return archive;
        }

        public static async Task<ZipArchive> CreateZipArchive(Stream stream, (string Path, object Data)[] contents)
        {
            var archive = new ZipArchive(stream, ZipArchiveMode.Update, true);
            foreach (var content in contents)
            {
                var entry = archive.CreateEntry(content.Path);
                await using var entryStream = entry.Open();
                await using var streamWriter = new StreamWriter(entryStream);
                switch (content.Data)
                {
                    case string dataString:
                    {
                        await streamWriter.WriteAsync(dataString);
                        break;
                    }
                    case Version dataVersion:
                    {
                        await streamWriter.WriteAsync(dataVersion.ToString());
                        break;
                    }
                    case byte[] dataBytes:
                        await entryStream.WriteAsync(dataBytes);
                        break;
                    default:
                        await JsonSerializer.SerializeAsync(entryStream, content.Data, content.Data.GetType());
                        break;
                }
            }

            return archive;
        }

        
        public static void AreEqual(ZipArchive expected, ZipArchive actualArchive)
        {
            async Task<string> GetStreamAsString(Stream stream)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                return await reader.ReadToEndAsync();
            }
            Assert.Multiple(async () =>
            {
                var expectedEntries = expected.Entries.ToList();

                foreach (var actualEntry in actualArchive.Entries)
                {
                    var expectedEntry = expected.Entries.FirstOrDefault(e => e.FullName == actualEntry.FullName);
                    if (expectedEntry == null)
                    {
                        Assert.Fail($"Additional entry {actualEntry.FullName} in actual archive.");
                        continue;
                    }

                    expectedEntries.Remove(expectedEntry);

                    await using var actualStream = actualEntry.Open();
                    await using var expectedStream = expectedEntry.Open();

                    switch (Path.GetExtension(expectedEntry.Name.ToLower()))
                    {
                        case ".json":
                        case ".txt":
                            var actualString = GetStreamAsString(actualStream);
                            var expectedString = GetStreamAsString(expectedStream);
                            Assert.AreEqual(expectedString, actualString);
                            continue;
                    }

                    FileAssert.AreEqual(expectedStream, actualStream, $"{actualEntry.FullName} file contents are not the same.");
                }

                if (expectedEntries.Count > 0)
                {
                    var files = string.Join(", ", expectedEntries.Select(e => e.FullName));
                    Assert.Fail($"Expected files missing from actual: {files}.");
                }
            });
        }

        public static async Task AreEqual(ZipArchive expected, string actualPath)
        {
            await using var fileStream = new FileStream(actualPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var actualArchive = new ZipArchive(fileStream, ZipArchiveMode.Update, false);

            AreEqual(expected, actualArchive);
        }
    }

}