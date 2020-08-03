using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

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
                switch (content.Data)
                {
                    case string dataString:
                    {
                        await using var streamWriter = new StreamWriter(entryStream);
                        await streamWriter.WriteAsync(dataString);
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
    }
}