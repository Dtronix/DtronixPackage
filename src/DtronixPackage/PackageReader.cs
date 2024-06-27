using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DtronixPackage;

public class PackageReader
{
    private readonly ZipArchive _archive;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly string _appName;

    public PackageReader(ZipArchive archive, 
        JsonSerializerOptions serializerOptions, 
        string appName)
    {
        _archive = archive;
        _serializerOptions = serializerOptions;
        _appName = appName;
    }

    /// <summary>
    /// Gets or creates a file inside this package.  Must close after usage.
    /// </summary>
    /// <param name="path">Path to the file inside the package.  Case sensitive.</param>
    /// <returns>Stream on existing file.  Null otherwise.</returns>
    public Stream? GetStream(string path)
    {
        var file = _archive.Entries.FirstOrDefault(f => f.FullName == _appName + "/" + path);
        return file?.Open();
    }

    /// <summary>
    /// Reads a string from the specified path inside the package.
    /// </summary>
    /// <param name="path">Path to the file inside the zip.  Case sensitive.</param>
    /// <returns>Stream on existing file.  Null otherwise.</returns>
    public async Task<string?> ReadString(string path)
    {
        await using var stream = GetStream(path);

        if (stream == null)
            return null;

        using var sr = new StreamReader(stream);
        return await sr.ReadToEndAsync();
    }

    /// <summary>
    /// Reads a JSON document from the specified path inside the package.
    /// </summary>
    /// <typeparam name="T">Type of JSON file to convert into.</typeparam>
    /// <param name="path">Path to open.</param>
    /// <returns>Decoded JSON object.</returns>
    public async ValueTask<T?> ReadJson<T>(string path)
    {
        await using var stream = GetStream(path);
        if (stream == null)
            return default;

        return await JsonSerializer.DeserializeAsync<T>(stream, _serializerOptions);
    }

    /// <summary>
    /// Open a JSON file inside the current application directory.
    /// </summary>
    /// <param name="path">Path to open.</param>
    /// <returns>Decoded JSON object.</returns>
    public Task<JsonDocument?> ReadJsonDocument(string path)
    {
        using var stream = GetStream(path);

        if (stream == null)
            return Task.FromResult((JsonDocument?)null);

        return JsonDocument.ParseAsync(stream)!;
    }

    /// <summary>
    /// Checks to see if a file exists inside the package.
    /// </summary>
    /// <param name="path">Path to find.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    public bool FileExists(string path)
    {
        path = _appName + "/" + path;
        return _archive.Entries.Any(e => e.FullName == path);
    }

    /// <summary>
    /// Returns all the file paths to files inside a package directory.
    /// </summary>
    /// <param name="path">Base path of the directory to search inside.</param>
    /// <returns>All paths to the files contained inside a package directory.</returns>
    public string[] DirectoryContents(string path)
    {
        var baseDir = _appName + "/";
        return _archive.Entries
            .Where(f => f.FullName.StartsWith(baseDir + path))
            .Select(e => e.FullName.Replace(baseDir, ""))
            .ToArray();
    }
}