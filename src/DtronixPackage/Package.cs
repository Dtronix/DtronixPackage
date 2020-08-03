using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using DtronixPackage.Logging;
using DtronixPackage.RecursiveChangeNotifier;
using DtronixPackage.Upgrades;

namespace DtronixPackage
{
    /// <summary>
    /// Manages reading, writing & multi-user usage of application package files.
    /// </summary>
    /// <typeparam name="TContent"></typeparam>
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public abstract class Package<TContent> : IDisposable
        where TContent : PackageContent, new()
    {
        private readonly string _appName;
        private readonly Version _appVersion;
        private readonly bool _preserveUpgrade;
        private readonly bool _useLockFile;
        private ZipArchive _openArchive;
        private List<string> _saveFileList;
        private FileStream _lockFile;
        private FileStream _openPackageStream;
        private ZipArchive _saveArchive;

        private string _lockFilePath;
        private readonly SemaphoreSlim _packageOperationSemaphore = new SemaphoreSlim(1, 1);
        private readonly List<ChangelogEntry> _changelog = new List<ChangelogEntry>();
        private readonly Timer _autoSaveTimer;
        private bool _autoSaveEnabled;
        private readonly Dictionary<object, ChangeListener> _registeredListeners 
            = new Dictionary<object, ChangeListener>();
        private int _autoSavePeriod = 60 * 1000;
        private int _autoSaveDueTime = 60 * 1000;
        private bool _disposed;
        private bool _isDataModified;
        private Version _openPackageVersion;

        protected static ILogger Logger;

        private static readonly Version PackageVersion 
            = typeof(Package<TContent>).Assembly.GetName().Version;

        /// <summary>
        /// Called upon closure of a package.
        /// </summary>
        public event EventHandler<EventArgs> Closed;

        /// <summary>
        /// Called upon closure of a package.
        /// </summary>
        public event EventHandler MonitoredChanged;

        /// <summary>
        /// Contains a log of all the times this package has been saved.
        /// </summary>
        public IReadOnlyList<ChangelogEntry> Changelog => _changelog.AsReadOnly();
        
        /// <summary>
        /// Application version of the opened package.
        /// </summary>
        public Version Version { get; private set; }
        
        /// <summary>
        /// If set to true, a ".BAK" package will be created with the previously saved package.
        /// </summary>
        public bool SaveBackupPackage { get; set; }

        /// <summary>
        /// Path to save the current package.
        /// </summary>
        public string SavePath { get; private set; }

        /// <summary>
        /// True if this package is in a read only state and can not be saved to the same package.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// If set to true, any changes detected by the monitor will notify that s save needs to occur.
        /// </summary>
        public bool IsMonitorEnabled { get; set; } = true;

        /// <summary>
        /// Contains a list of upgrades which will be performed on older versions of packages.
        /// Add to this list to include additional upgrades.  Will execute in the order listed.
        /// </summary>
        protected List<PackageUpgrade> Upgrades { get; } = new List<PackageUpgrade>();

        /// <summary>
        /// True if the package has auto-save turned on.
        /// </summary>
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                Logger?.ConditionalTrace($"AutoSaveEnabled = {value}.");
                _autoSaveEnabled = value;
                SetAutoSaveTimer();
            }
        }
        /// <summary>
        /// True if the data has been modified since the last save.
        /// </summary>
        public bool IsDataModified
        {
            get => _isDataModified;
            internal set
            {
                _isDataModified = value;
                IsDataModifiedSinceAutoSave = value;

                // Only invoke the MonitorChanged event if there are changes.
                if(value)
                    MonitoredChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private TContent _content;

        public TContent Content
        {
            get => _content;
            protected set
            {
                if(_content != null)
                    MonitorDeregister(_content);

                _content = value; 

                if(_content != null) 
                    MonitorRegister(value);
            }
        }

        /// <summary>
        /// Time this package was initially opened/created.
        /// </summary>
        protected DateTime OpenTime { get; private set; }

        /// <summary>
        /// Path to the auto-save package for this package.
        /// </summary>
        protected string AutoSavePath { get; private set; }
        
        /// <summary>
        /// Options for serialization of all objects.
        /// </summary>
        protected JsonSerializerOptions JsonSerializerOptions { get; }

        /// <summary>
        /// True if the data has been modified since the last auto save. Otherwise false.
        /// </summary>
        internal bool IsDataModifiedSinceAutoSave;

        /// <summary>
        /// Creates & configures an instance of DtronixPackage.
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="appVersion">
        /// Currently running application's version.
        /// </param>
        /// <param name="preserveUpgrade">
        /// If set to true and a package opened is set on a previous version than specified in CurrentVersion,
        /// A copy of all the files in the ApplicationName directory is copied into a backup directory named
        /// ApplicationName-backup-CurrentVersion.
        /// </param>
        /// <param name="useLockFile">
        /// If set to true and a lockfile exists "filename.ext.lock", then the opening process is aborted and
        /// an opening error is thrown.
        /// </param>
        protected Package(string appName, Version appVersion, bool preserveUpgrade, bool useLockFile)
        {
            _appName = appName;
            _appVersion = appVersion;
            _preserveUpgrade = preserveUpgrade;
            _useLockFile = useLockFile;

            // Create a dummy time to allow for 
            OpenTime = DateTime.Now;

            _autoSaveTimer = new Timer(AutoSaveElapsed);

            JsonSerializerOptions = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                WriteIndented = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = null,
                AllowTrailingCommas = true,
            };

        }

        /// <summary>
        /// Method called when opening a package file and to run the application specific opening code.
        /// By default, will read content.json and copy the contents to the Content object.
        /// </summary>
        /// <param name="isUpgrade"></param>
        /// <returns>True of successful opening. False otherwise.</returns>
        protected virtual async Task<bool> OnOpen(bool isUpgrade)
        {
            try
            {
                Content = await ReadJson<TContent>("content.json");
            }
            catch (Exception e)
            {
                Logger?.Error(e, "Unable to parse content.json file.");
                Content = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Method called when saving.  By default, will save the Content object to contents.json
        /// </summary>
        protected virtual async Task OnSave()
        {
            try
            {
                await WriteJson("content.json", Content);
            }
            catch (Exception e)
            {
                Logger?.Error(e, "Unable to write content.json file.");
            }
        }

        protected abstract string OnTempFilePathRequest(string fileName);

        /// <summary>
        /// Gets or creates a file inside this package.  Must close after usage.
        /// </summary>
        /// <param name="path">Path to the file inside the package.  Case sensitive.</param>
        /// <returns>Stream on existing file.  Null otherwise.</returns>
        protected internal Stream GetStream(string path)
        {
            var file = _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/" + path);
            return file?.Open();
        }

        /// <summary>
        /// Reads a string from the specified path inside the package.
        /// </summary>
        /// <param name="path">Path to the file inside the zip.  Case sensitive.</param>
        /// <returns>Stream on existing file.  Null otherwise.</returns>
        protected internal async Task<string> ReadString(string path)
        {
            await using var stream = GetStream(path);
            using var sr = new StreamReader(stream);
            return await sr.ReadToEndAsync();
        }

        /// <summary>
        /// Reads a JSON document from the specified path inside the package.
        /// </summary>
        /// <typeparam name="T">Type of JSON file to convert into.</typeparam>
        /// <param name="path">Path to open.</param>
        /// <returns>Decoded JSON object.</returns>
        protected internal async ValueTask<T> ReadJson<T>(string path)
        {
            await using var stream = GetStream(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions);
        }

        /// <summary>
        /// Open a JSON file inside the current application directory.
        /// </summary>
        /// <param name="path">Path to open.</param>
        /// <returns>Decoded JSON object.</returns>
        protected internal Task<JsonDocument> ReadJsonDocument(string path)
        {
            using var stream = GetStream(path);
            return JsonDocument.ParseAsync(stream);
        }

        /// <summary>
        /// Checks to see if a file exists inside the package.
        /// </summary>
        /// <param name="path">Path to find.</param>
        /// <returns>True if the file exists, false otherwise.</returns>
        protected internal bool FileExists(string path)
        {
            path = _appName + "/" + path;
            return _openArchive.Entries.Any(e => e.FullName == path);
        }

        /// <summary>
        /// Returns all the file paths to files inside a package directory.
        /// </summary>
        /// <param name="path">Base path of the directory to search inside.</param>
        /// <returns>All paths to the files contained inside a package directory.</returns>
        protected internal string[] DirectoryContents(string path)
        {
            var baseDir = _appName + "/";
            return _openArchive.Entries
                .Where(f => f.FullName.StartsWith(baseDir + path))
                .Select(e => e.FullName.Replace(baseDir, ""))
                .ToArray();
        }

        /// <summary>
        /// Writes an object to a JSON file at the specified path inside the package.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="json"></param>
        protected internal async Task WriteJson<T>(string path, T json)
        {
            await using var entityStream = CreateEntityStream(path, true);
            await JsonSerializer.SerializeAsync(entityStream, json, JsonSerializerOptions);
        }

        /// <summary>
        /// Writes a string to a text file at the specified path inside the package.
        /// </summary>
        /// <param name="path">Path to save.</param>
        /// <param name="text">String of data to save.</param>
        protected internal async Task WriteString(string path, string text)
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
        protected internal async Task WriteStream(string path, Stream stream)
        {
            await using var entityStream = CreateEntityStream(path, true);
            await stream.CopyToAsync(entityStream);
        }

        /// <summary>
        /// Returns a stream to the file at the specified location. Blocks other writes until stream is closed.
        /// </summary>
        /// <param name="path">Path to save.</param>
        /// <returns>Writable stream.</returns>
        protected Stream WriteGetStream(string path)
        {
            return CreateEntityStream(path, true);
        }

        /// <summary>
        /// Saves a stream of data into the current application directory.
        /// </summary>
        /// <param name="path">Path to save.</param>
        /// <param name="encoder">Encoder data to save.</param>
        protected internal void WriteBitmapEncoder(string path, BitmapEncoder encoder)
        {
            // No compression for images.
            using var entityStream = CreateEntityStream(path, true, CompressionLevel.NoCompression);
            using var ms = new MemoryStream();

            // Save to the memory stream
            encoder.Save(ms);
            ms.Position = 0;

            // Copy to the zip entity.
            ms.CopyTo(entityStream);
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
        private Stream CreateEntityStream(string path,
            bool prefixApplicationName,
            CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (_saveFileList == null)
                throw new Exception("Can not access save functions while outside of a OnSave call");

            var entityPath = prefixApplicationName
                ? _appName + "/" + path
                : path;

            var entity = _saveArchive.CreateEntry(entityPath, compressionLevel);
            _saveFileList.Add(entity.FullName);

            return entity.Open();
        }

        /// <summary>
        /// Opens a package.
        /// </summary>
        /// <param name="path">Package path to open.</param>
        /// <param name="openReadOnly">
        /// Specifies that the package will be open in read-only mode and can not be saved back
        /// to the source file.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for opening.</param>
        /// <returns>True if opening was successful, otherwise false.</returns>
        public async Task<PackageOpenResult> Open(
            string path,
            bool openReadOnly = false, 
            CancellationToken cancellationToken = default)
        {
            if (_openPackageStream != null)
                throw new InvalidOperationException("Can not open package when a package is already open");

            await _packageOperationSemaphore.WaitAsync(cancellationToken);

            PackageOpenResult returnValue = null;

            try
            {

                SavePath = path;
                _lockFilePath = SavePath + ".lock";
                IsReadOnly = openReadOnly;

                // Read the lock file.
                if (!openReadOnly)
                {
                    var (lockExists, lockFile) = await LockExists();

                    if(lockExists)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.Locked, null, lockFile);
                }

                try
                {
                    // Copy the package into memory for use.
                    // Retain the package lock to ensure the file does not get changed.
                    _openPackageStream = new FileStream(
                        path,
                        FileMode.Open,
                        openReadOnly ? FileAccess.Read : FileAccess.ReadWrite,
                        FileShare.ReadWrite);
                }
                catch (FileNotFoundException e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.FileNotFound, e);
                }
                catch (DirectoryNotFoundException e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.FileNotFound, e);
                }
                catch (DriveNotFoundException e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.FileNotFound, e);
                }
                catch (PathTooLongException e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.FileNotFound, e);
                }
                catch (UnauthorizedAccessException e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.PermissionFailure, e);
                }
                catch (IOException e) when ((e.HResult & 0x0000FFFF) == 32)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Locked, e);
                }
                catch (Exception e)
                {
                    Logger?.Error(e, "Unknown error while opening file.");
                    return returnValue = new PackageOpenResult(PackageOpenResultType.UnknownFailure, e);
                }

                // Read the entire contents to memory for usage in archive Update mode.
                Stream openPackageStreamCopy = new MemoryStream();
                await _openPackageStream.CopyToAsync(openPackageStreamCopy, cancellationToken);

                if (openReadOnly)
                {
                    _openPackageStream.Close();
                    _openPackageStream = null;
                }

                try
                {
                    _openArchive = new ZipArchive(openPackageStreamCopy, ZipArchiveMode.Update);

                    // Read the package version number
                    var packageVersionEntry = _openArchive.Entries.FirstOrDefault(f => f.FullName == "version");

                    if (packageVersionEntry == null)
                    {                   
                        // If the package version is not set, this is an older file which will need to be upgraded appropriately.
                        _openPackageVersion = new Version(0, 0, 0);
                    }
                    else
                    {
                        using var reader = new StreamReader(packageVersionEntry.Open());
                        _openPackageVersion = new Version(await reader.ReadToEndAsync());
                    }

                    // Read the version information in the application directory.
                    var versionEntity = _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/version");

                    // If the versionEntity is empty, this usually means that this package was not created by the same
                    // application currently opening the package.
                    if (versionEntity == null)
                    {

                        // Check to see if there is a version file in the entire package.  If there is, there is a high
                        // probability that this is a Package file, just used by another application's package system.
                        if(_openArchive.Entries.Any(f => f.FullName.EndsWith("/version")))
                            return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleApplication);
                        else
                            return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted);
                    }

                    using (var reader = new StreamReader(versionEntity.Open()))
                    {
                        Version = new Version(await reader.ReadToEndAsync());
                    }

                    // Don't allow opening of newer packages on older applications.
                    if (_appVersion < Version)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleVersion);
                }
                catch(Exception e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted, e);
                }

                // Perform any required package upgrades.
                if (_openPackageVersion < PackageVersion)
                {
                    var packageUpgrades = new PackageUpgrade[]
                    {
                        new PackageUpgrade_1_1_0(),
                    };

                    var upgradeResult = await ApplyUpgrades(packageUpgrades, true, _openPackageVersion);

                    // If the result is not null, the upgrade failed.
                    if(upgradeResult != null)
                        return returnValue = upgradeResult;
                }

                // Try to open the modified log file. It may not exist in older versions.
                var changelogEntry =
                    _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/changelog.json");

                if (changelogEntry != null)
                {
                    try
                    {
                        _changelog.Clear();
                        await using var stream = changelogEntry.Open();
                        var logItems = await JsonSerializer.DeserializeAsync<ChangelogEntry[]>(
                            stream, 
                            null, 
                            cancellationToken);

                        foreach (var changelogItem in logItems)
                            _changelog.Add(changelogItem);
                        
                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e, "Could not parse save log.");
                        _changelog.Clear();
                    }
                }

                // Perform upgrades at this time.
                if (_appVersion > Version)
                {
                    // "Renames" existing contents of this application's directory to a backup directory 
                    if (_preserveUpgrade)
                    {
                        var entries = _openArchive.Entries.ToArray();
                        foreach (var entry in entries)
                        {
                            // If there is a version miss-match, save the original files in a modified
                            // directory to retrieve in-case of corruption.
                            if (!entry.FullName.StartsWith(_appName + "/")) 
                                continue;

                            // Renames all the sub-files if there was a version mis-match on open.
                            var newName = _appName + $"-backup-{Version}/{entry.FullName}";

                            var saveEntry = _openArchive.CreateEntry(newName);

                            // Copy the last write times to be accurate.
                            saveEntry.LastWriteTime = entry.LastWriteTime;

                            // Copy the streams.
                            await using var openedArchiveStream = entry.Open();
                            await using var saveEntryStream = saveEntry.Open();
                            await openedArchiveStream.CopyToAsync(saveEntryStream, cancellationToken);
                        }
                    }

                    var upgradeResult = await ApplyUpgrades(Upgrades, false, Version);

                    // If the result is not null, the upgrade failed.
                    if(upgradeResult != null)
                        return returnValue = upgradeResult;
                }

                // If we are not in read-only mode and we are using lock files, create a lock file
                // and if it failed, stop opening.
                if (!openReadOnly && _useLockFile && !await SetLockFile())
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Locked);
                }

                OpenTime = DateTime.Now;
                AutoSavePath = null;

                return returnValue = await OnOpen(Version != _appVersion)
                    ? PackageOpenResult.Success
                    : new PackageOpenResult(PackageOpenResultType.ReadingFailure);

            }
            catch (Exception e)
            {
                return returnValue = new PackageOpenResult(PackageOpenResultType.UnknownFailure, e);
            }
            finally
            {
                // Perform cleanup if the open was not successful.
                // Do not fire the close event since it was not successfully opened in the first place.
                if (returnValue != PackageOpenResult.Success)
                    CloseInternal(false);

                _packageOperationSemaphore.Release();
            }
        }

        
        /// <summary>
        /// Updates the currently open lock file with the current username & DateTime.
        /// </summary>
        private async Task<bool> SetLockFile()
        {
            // Create the lock file and hold on to the lock until closed.
            if (_lockFile == null)
            {
                try
                {
                    // Create the lock file and hold on to the lock until closed.
                    _lockFile = new FileStream(_lockFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
                }
                catch
                {
                    return false;
                }

                if (!_lockFile.CanWrite)
                    return false;
            }

            // Reset our write position to the beginning.
            _lockFile.Position = 0;

            await JsonSerializer.SerializeAsync(_lockFile, new FileLockContents
            {
                Username = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                DateOpened = DateTime.Now
            });

            // Flush the stream to the lock file for reading by others.
            await _lockFile.FlushAsync();

            return true;
        }

        /// <summary>
        /// Check to see if the lock exists.  If it does, try to delete it to see if the lock file is truly in use.
        /// If the file can be deleted, create a new lock file.
        /// If it can not be deleted, notify the user and return false.
        /// </summary>
        /// <returns>True if the lock file exists, false otherwise.  Lock file information if it was read.</returns>
        private async Task<(bool lockExists, FileLockContents lockFile)> LockExists()
        {
            // Create/read the lock file.
            if (!File.Exists(_lockFilePath)) 
                return (false, null);

            try
            {
                // If we can delete the lock file, then a lock is not being held on the file
                // Thus, the lock can be ignored and treated as an uncleaned artifact.
                File.Delete(_lockFilePath);
                return (false, null);
            }
            catch
            {
                // The file could not be deleted, so we continue trying to read the file.
            }

            try
            {
                await using var lockFileStream
                    = new FileStream(_lockFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                var lockFile = await JsonSerializer.DeserializeAsync<FileLockContents>(lockFileStream);
                return (true, lockFile);
            }
            catch
            {
                // The lock file is locked by another process or the file was malformed.
                return (true, null);
            }
        }

        /// <summary>
        /// Saves the current package to the specified path.
        /// </summary>
        /// <param name="path">Path to output the package.</param>
        public async Task<PackageSaveResult> Save(string path)
        {
            // Check the path if we are not auto-saving.
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            Logger?.ConditionalTrace("Requesting _packageOperationSemaphore");
            // Ensure we are not already saving.
            await _packageOperationSemaphore.WaitAsync();

            Logger?.ConditionalTrace("Acquired _packageOperationSemaphore");

            _saveFileList = new List<string>();

            // Determine if we have changed save the save path location since the last save.
            if (path != SavePath)
            {
                // If the package stream is open and we are changing the path, close the old stream.
                if (_openPackageStream != null)
                {
                    _openPackageStream.Close();
                    _openPackageStream = null;
                }

                SavePath = path;

                // If the package is new and not saved yet, a lock file has not been created.
                if (_lockFile != null)
                {
                    // If the _lockFile variable is set, and the path has changed, this means 
                    // that there is an extra lock file which needs to be removed.  Try to remove it.
                    try
                    {
                        _lockFile.Close();
                        _lockFile = null;
                        File.Delete(_lockFilePath);
                    }
                    catch (Exception e)
                    {
                        Logger?.Info(e, $"Unable to remove lock file at: {_lockFilePath}.");
                    }
                }

                _lockFilePath = path + ".lock";
            }

            // Lock file creation.
            try
            {
                if (_useLockFile && _lockFile == null)
                {
                    var (lockExists, lockFile) = await LockExists();

                    if(lockExists)
                        return new PackageSaveResult(PackageSaveResultType.Locked, null, lockFile);

                    if (!await SetLockFile())
                        return new PackageSaveResult(PackageSaveResultType.Locked);
                }
                
                return await SaveInternal(false);
            }
            catch (IOException e)
            {
                return new PackageSaveResult(PackageSaveResultType.Failure, e);
            }
            finally
            {
                _saveFileList = null;
            }
        }

        /// <summary>
        /// Core save functionality.
        /// </summary>
        /// <param name="autoSave">
        /// Set to true to bypass saving to the SavePath and instead save the current package to the temp directory.
        /// </param>
        /// <returns>Result of saving</returns>
        private async Task<PackageSaveResult> SaveInternal(bool autoSave)
        {
            Logger?.ConditionalTrace($"SaveInternal(autoSave:{autoSave})");
            PackageSaveResult returnValue = null;
            try
            {
                // TODO: Reuse existing archive when saving from a read-only package.  Prevents duplication of the MS.
                var saveArchiveMemoryStream = new MemoryStream();

                using (_saveArchive = new ZipArchive(saveArchiveMemoryStream, ZipArchiveMode.Create, true))
                {
                    // Write application version file
                    await using (var packageVersionStream = CreateEntityStream("version", false))
                    {
                        await using var writer = new StreamWriter(packageVersionStream);
                        await writer.WriteAsync(PackageVersion.ToString());
                    }

                    await OnSave();

                    // Write package version file
                    await WriteString("version", _appVersion.ToString());

                    var log = new ChangelogEntry(autoSave ? ChangelogEntryType.AutoSave : ChangelogEntryType.Save);

                    // Update the save log.
                    _changelog.Add(log);

                    // Write the save log.
                    await WriteJson("changelog.json", _changelog);

                    // If this is an auto save, we do not want to continually add auto save logs.
                    if (autoSave)
                        _changelog.Remove(log);

                    if (_openArchive != null)
                    {
                        foreach (var openedArchiveEntry in _openArchive.Entries)
                        {
                            if (_saveFileList.Contains(openedArchiveEntry.FullName))
                                continue;

                            var saveEntry = _saveArchive.CreateEntry(openedArchiveEntry.FullName);

                            // Copy the last write times to be accurate.
                            saveEntry.LastWriteTime = openedArchiveEntry.LastWriteTime;

                            // Copy the streams.
                            await using var openedArchiveStream = openedArchiveEntry.Open();
                            await using var saveEntryStream = saveEntry.Open();
                            await openedArchiveStream.CopyToAsync(saveEntryStream);
                        }
                    }
                    
                }

                _saveArchive = null;

                // Only save a backup package if we are not performing an auto save.
                if (SaveBackupPackage && !autoSave)
                {
                    Logger?.ConditionalTrace("Saving backup package.");
                    var bakPackage = SavePath + ".bak";
                    try
                    {
                        Logger?.ConditionalTrace($"Checking for existence of {bakPackage} backup .");
                        if (File.Exists(bakPackage))
                        {
                            Logger?.ConditionalTrace("Found backup package.");
                            File.SetAttributes(bakPackage, FileAttributes.Normal);
                            File.Delete(bakPackage);
                            Logger?.ConditionalTrace("Deleted backup package.");
                        }

                        // Close the lock held on the file.  On initial save, there is nothing to close.
                        _openPackageStream?.Close();
                        _openPackageStream = null;

                        Logger?.ConditionalTrace("Closed _openPackageStream.");
                        Logger?.ConditionalTrace($"Checking for existence of existing save package {SavePath}");

                        if (File.Exists(SavePath))
                        {
                            File.Move(SavePath, bakPackage);
                            Logger?.ConditionalTrace($"Found existing save package and renamed to {bakPackage}");
                        }
                    }
                    catch (Exception e)
                    {
                        return returnValue = new PackageSaveResult(PackageSaveResultType.Failure, e);
                    }
                }

                if (!autoSave)
                {
                    // Create a new package/overwrite existing if a backup was not created.
                    // Retain the lock on the package.
                    if (_openPackageStream == null)
                    {
                        // Create a new source package.
                        _openPackageStream = new FileStream(SavePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    }
                    else
                    {
                        // Set the package file length to 0 to reset for writing.
                        _openPackageStream.Position = 0;
                        _openPackageStream.SetLength(0);
                    }
                }

                if (autoSave)
                {
                    Logger?.ConditionalTrace($"Creating AutoSave package {AutoSavePath}.");
                }

                var destinationStream = autoSave ? File.Create(AutoSavePath) : _openPackageStream;

                if (autoSave)
                {
                    Logger?.ConditionalTrace($"Created AutoSave package {AutoSavePath}.");
                }

                if (destinationStream == null)
                {
                    Logger?.ConditionalTrace("Could not create/use destination stream.");
                    return returnValue = new PackageSaveResult(PackageSaveResultType.Failure);
                }

                saveArchiveMemoryStream.Seek(0, SeekOrigin.Begin);

                // Copy and flush the contents of the save file.
                await saveArchiveMemoryStream.CopyToAsync(destinationStream);
                await destinationStream.FlushAsync();

                // If we were auto-saving, close the destination save and return true.
                // We do not want to effect the current state of the rest of the package.
                if (autoSave)
                {
                    destinationStream.Close();
                    Logger?.ConditionalTrace("Closed AutoSave destination stream.");
                    return returnValue = PackageSaveResult.Success;
                }

                saveArchiveMemoryStream.Seek(0, SeekOrigin.Begin);

                // This implicitly closes the inner stream.
                _openArchive?.Dispose();

                // Save the new zip archive stream to the opened archive stream.
                _openArchive = new ZipArchive(saveArchiveMemoryStream, ZipArchiveMode.Read, true);

                Version = _appVersion;

                return returnValue = PackageSaveResult.Success;
            }
            catch (Exception e)
            {
                // Catch all for anything going completely crazy.
                return returnValue = new PackageSaveResult(PackageSaveResultType.Failure, e);
            }
            finally
            {
                // If we were successful in saving, set the modified variable to false.
                // If we are auto-saving, don't change these variables since the package still needs
                // to actually be saved to the destination.
                if (returnValue == PackageSaveResult.Success && autoSave == false)
                {
                    IsDataModified = false;

                    // Since the package was saved, the package is no longer in read-only mode.
                    IsReadOnly = false;
                }

                Logger?.ConditionalTrace($"InternalSave return: {returnValue}");

                _saveFileList = null;

                Logger?.ConditionalTrace("Released _packageOperationSemaphore");
                _packageOperationSemaphore.Release();
            }
        }

        /// <summary>
        /// Sets the due time and period for auto-saves.  See <see cref="System.Threading.Timer"/>. If either the
        /// time or the period are greater than -1, the auto save is enabled otherwise, it is disabled.
        /// </summary>
        /// <param name="dueTime">
        /// The amount of time to delay before the invoking auto save, in milliseconds.
        /// Specify Infinite to prevent auto save from restarting. Specify zero (0) to trigger an auto save immediately.
        /// </param>
        /// <param name="period">
        /// The time interval between auto save invocations, in milliseconds.
        /// Specify Infinite to disable periodic auto saves.
        /// </param>
        public async Task ConfigureAutoSave(int dueTime, int period)
        {
            Logger?.ConditionalTrace($"ConfigureAutoSave(dueTime:{dueTime}, period:{period})");

            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), "Value must be -1 or higher.");

            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), "Value must be -1 or higher.");

            _autoSaveDueTime = dueTime;
            _autoSavePeriod = period;

            if (dueTime == 0)
            {
                Logger?.ConditionalTrace("Running auto save immediately.");
                // If we call for it to save once immediately, call now.
                await AutoSave(true);

                // If this is a periodical save, push the due time up to the period time since we 
                // just synchronously called the AutoSaveElapsed method.
                if (period > -1)
                {
                    Logger?.ConditionalTrace($"Forward setting since AutoSave was just called. _autoSaveDueTime = {period}");
                    _autoSaveDueTime = period;
                }
                else
                {
                    // If we are not recurring, then do nothing else.
                    return;
                }
            }

            // If the auto save is configured to run at least once, enable the auto save.  Otherwise disable.
            AutoSaveEnabled = dueTime >= 0 || period >= 0;
        }

        private async Task AutoSave(bool synchronous)
        {
            Logger?.ConditionalTrace($"AutoSave(synchronous:{synchronous})");

            // If the AutoSave path has not been set, set it now.
            AutoSavePath ??= OnTempFilePathRequest(OpenTime.ToString("yyyy-MM-dd-T-HH-mm-ss-fff") + ".sav");

            // Ensure that auto-save is still enabled & that the content has been modified.
            if ((AutoSaveEnabled || synchronous) && IsDataModifiedSinceAutoSave)
            {

                Logger?.ConditionalTrace("Requesting _packageOperationSemaphore");
                // Ensure we are not already saving.
                await _packageOperationSemaphore.WaitAsync();
                Logger?.ConditionalTrace("Acquired _packageOperationSemaphore");

                _saveFileList = new List<string>();
                var saveResult = await SaveInternal(true);

                if (saveResult == PackageSaveResult.Success)
                {
                    // Set the internal variable to ensure that we do not auto-save more than what is required.
                    IsDataModifiedSinceAutoSave = false;
                }
                else
                {
                    Logger?.Error(saveResult.Exception, $"Could not successfully auto-save package {saveResult.SaveResult}");
                }
            }
        }

        private void AutoSaveElapsed(object state)
        {
            Logger?.ConditionalTrace("AutoSaveElapsed()");
            _ = AutoSave(false);
        }

        private void SetAutoSaveTimer()
        {
            // Update the current timer if it is running.
            if (_autoSaveEnabled)
            {
                Logger?.ConditionalTrace($"_autoSaveDueTime = {_autoSaveDueTime}; _autoSavePeriod = {_autoSavePeriod}");
                _autoSaveTimer.Change(_autoSaveDueTime, _autoSavePeriod);
            }
            else
            {
                Logger?.ConditionalTrace("_autoSaveDueTime = -1; _autoSavePeriod = -1");
                _autoSaveTimer.Change(-1, -1);
            }
        }

        private async Task<PackageOpenResult> ApplyUpgrades(
            IList<PackageUpgrade> upgrades, 
            bool packageUpgrade, 
            Version compareVersion)
        {
            var currentVersion = packageUpgrade ? _openPackageVersion : Version;

            foreach (var upgrade in upgrades.Where(upgrade => upgrade.Version > compareVersion))
            {
                try
                {
                    // Attempt to perform the upgrade
                    if (!await upgrade.Upgrade(_openArchive))
                    {
                        // Upgrade soft failed, log it and notify the opener.
                        Logger?.Error($"Unable to perform{(packageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.Version}.");
                        return new PackageOpenResult(PackageOpenResultType.UpgradeFailure);
                    }

                    _changelog.Add(new ChangelogEntry(packageUpgrade
                        ? ChangelogEntryType.PackageUpgrade
                        : ChangelogEntryType.ApplicationUpgrade)
                    {
                        Note = packageUpgrade 
                            ? $"Package upgrade from {currentVersion} to {upgrade.Version}"
                            : $"Application upgrade from {currentVersion} to {upgrade.Version}"
                    });

                    currentVersion = upgrade.Version;
                }
                catch (Exception e)
                {
                    // Upgrade hard failed.
                    Logger?.Error(e, $"Unable to perform{(packageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.Version}.");
                    return new PackageOpenResult(PackageOpenResultType.UpgradeFailure, e);
                }

                // Since we did perform an upgrade, set set that the package has been changed.
                IsDataModified = true;
            }

            return null;
        }

        protected void MonitorRegister<T>(T obj)
            where T : INotifyPropertyChanged
        {
            if (obj == null)
                return;

            // Do not double register the same object.
            if (_registeredListeners.ContainsKey(obj))
                return;
            
            var listener = ChangeListener.Create(obj);
            listener.CollectionChanged += MonitorListenerOnCollectionChanged;
            listener.PropertyChanged += MonitorListenerOnPropertyChanged;
            _registeredListeners.Add(obj, listener);

        }

        protected void MonitorDeregister<T>(T obj)
            where T : INotifyPropertyChanged
        {
            if (obj == null)
                return;

            // Do nothing if it is not registered.
            if (!_registeredListeners.TryGetValue(obj, out var listener))
                return;

            _registeredListeners.Remove(obj);
            listener.CollectionChanged -= MonitorListenerOnCollectionChanged;
            listener.PropertyChanged -= MonitorListenerOnPropertyChanged;
            listener.Dispose();
        }

        protected virtual void MonitorListenerOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(IsMonitorEnabled)
                DataModified();
        }

        protected virtual  void MonitorListenerOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(IsMonitorEnabled)
                DataModified();
        }

        /// <summary>
        /// Whatever changes are made inside the action are not reported back to the monitor.
        /// </summary>
        /// <param name="action">Action to perform.</param>
        public void MonitorIgnore(Action action)
        {
            var originalValue = IsMonitorEnabled;
            IsMonitorEnabled = false;
            action?.Invoke();
            IsMonitorEnabled = originalValue;
        }

        /// <summary>
        /// Call when the package has been changed.
        /// </summary>
        protected void DataModified()
        {
            Logger?.ConditionalTrace("DataModified()");
            IsDataModified = true;
        }
        
        /// <summary>
        /// Closes the currently open package.
        /// </summary>
        public void Close()
        {
            CloseInternal(true);
        }

        /// <summary>
        /// Closes the currently open Package.
        /// </summary>
        private void CloseInternal(bool invokeClosed)
        {
            if (_useLockFile)
            {
                _lockFile?.Close();

                try
                {
                    if (File.Exists(_lockFilePath))
                        File.Delete(_lockFilePath);
                }
                catch (IOException)
                {
                    // Usually means that another application has this file opened for reading.
                }
            }

            _openArchive?.Dispose();
            _openArchive = null;
            _openPackageStream?.Dispose();
            _openPackageStream = null;
            _saveArchive?.Dispose();
            _saveArchive = null;
            _saveFileList = null;
            _lockFile?.Close();
            _lockFile = null;
            _openPackageStream?.Close();
            _openPackageStream = null;
            _lockFilePath = null;
            _changelog.Clear();
            _openPackageVersion = null;
            foreach (var registeredListener in _registeredListeners)
                registeredListener.Value.Dispose();

            _registeredListeners.Clear();

            Version = null;
            SavePath = null;

            IsReadOnly = false;

            // Disable auto-save only if it is enabled.
            if(AutoSaveEnabled)
                AutoSaveEnabled = false;

            IsDataModified = false;
            OpenTime = default;
            AutoSavePath = null;

            if(invokeClosed)
                Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                CloseInternal(false);
            }
        }

        ~Package()
        {
            Dispose(false);
        }
    }
}