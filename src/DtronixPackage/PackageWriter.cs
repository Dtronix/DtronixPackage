using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DtronixPackage;

public class PackageWriter
{
    private readonly ZipArchive _archive;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _appName;
        
    internal List<string> FileList { get; } = new List<string>();

    public PackageWriter(
        ZipArchive archive,
        JsonSerializerOptions serializerOptions,
        string appName)
    {
        _archive = archive;
        _serializerOptions = serializerOptions;
        _appName = appName;
    }


    /// <summary>
    /// Writes an object to a JSON file at the specified path inside the package.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="json"></param>
    public async Task WriteJson<T>(string path, T json)
    {
        await using var entityStream = CreateEntityStream(path, true);
        await JsonSerializer.SerializeAsync(entityStream, json, _serializerOptions);
    }

    /// <summary>
    /// Writes a string to a text file at the specified path inside the package.
    /// </summary>
    /// <param name="path">Path to save.</param>
    /// <param name="text">String of data to save.</param>
    public async Task Write(string path, string text)
    {
        await using var entityStream = CreateEntityStream(path, true);
        await using var writer = new StreamWriter(entityStream);

        await writer.WriteAsync(text);
        await writer.FlushAsync();
    }

    /// <summary>
    /// Writes a stream to a file at the specified path inside the package.
    /// </summary>
    /// <param name="path">Path to save.</param>
    /// <param name="stream">Stream of data to save.</param>
    public async Task Write(string path, Stream stream)
    {
        await using var entityStream = CreateEntityStream(path, true);
        await stream.CopyToAsync(entityStream);
    }

    /// <summary>
    /// Writes a stream to a file at the specified path inside the package.
    /// </summary>
    /// <param name="path">Path to save.</param>
    /// <param name="data">Bytes of data to save.</param>
    public async Task Write(string path, ReadOnlyMemory<byte> data)
    {
        await using var entityStream = CreateEntityStream(path, true);
        await entityStream.WriteAsync(data);
    }

    /// <summary>
    /// Returns a stream to the file at the specified location. Blocks other writes until stream is closed.
    /// </summary>
    /// <param name="path">Path to save.</param>
    /// <returns>Writable stream.</returns>
    public Stream GetStream(string path)
    {
        return CreateEntityStream(path, true);
    }

    /// <summary>
    /// Returns a stream to the file at the specified location. Blocks other writes until stream is closed.
    /// </summary>
    /// <param name="path">Path to save.</param>
    /// <param name="compressionLevel">Sets the compression level for the file.</param>
    /// <returns>Writable stream.</returns>
    protected Stream WriteGetStream(string path, 
        CompressionLevel compressionLevel)
    {
        return CreateEntityStream(path, true, compressionLevel);
    }

        
    /// <summary>
    /// Helper function to create a new entity at the specified path and return a stream.
    /// Stream must be closed.
    /// </summary>
    /// <param name="path">Path to the entity.  Note, this prefixes the application name to the path.</param>
    /// <param name="prefixApplicationName">
    /// Set to true to have the application name be prefixed to all the passed paths.
    /// </param>
    /// <param name="compressionLevel">Compression level for the entity</param>
    /// <returns>Stream to the entity.  Must be closed.</returns>
    internal Stream CreateEntityStream(string path,
        bool prefixApplicationName,
        CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        var entityPath = prefixApplicationName
            ? _appName + "/" + path
            : path;

        var entity = _archive.CreateEntry(entityPath, compressionLevel);
        FileList.Add(entity.FullName);

        return entity.Open();
    }
}