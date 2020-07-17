﻿using System;
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
using DtronixPackage.RecursiveChangeNotifier;
using NLog;

namespace DtronixPackage
{
    [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
    public abstract class Package<TContent> : IDisposable
        where TContent : FileContent, new()
    {
        private readonly string _appName;
        private readonly Version _appVersion;
        private readonly bool _preserveUpgrade;
        private readonly bool _useLockFile;
        private ZipArchive _openArchive;
        private List<string> _saveFileList;
        private FileStream _lockFile;
        private FileStream _openFileStream;
        private ZipArchive _saveArchive;

        private string _lockFilePath;
        private readonly SemaphoreSlim _fileOperationSemaphore = new SemaphoreSlim(1, 1);
        private List<SaveLogItem> _saveLog = new List<SaveLogItem>();
        private readonly Timer _autoSaveTimer;
        private bool _autoSaveEnabled;
        private readonly Dictionary<object, ChangeListener> _registeredListeners = new Dictionary<object, ChangeListener>();
        private int _autoSavePeriod = 60 * 1000;
        private int _autoSaveDueTime = 60 * 1000;
        private bool _disposed;
        private bool _isDataModified;

        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly Version FileVersion;

        /// <summary>
        /// Called upon closure of a file.
        /// </summary>
        public event EventHandler<EventArgs> Closed;

        /// <summary>
        /// Called upon closure of a file.
        /// </summary>
        public event EventHandler MonitoredChanged;

        /// <summary>
        /// Contains a log of all the times this file has been saved.
        /// </summary>
        public IReadOnlyList<SaveLogItem> SaveLog => _saveLog.AsReadOnly();
        
        /// <summary>
        /// Version of the opened file.
        /// </summary>
        public Version OpenVersion { get; private set; }
        
        /// <summary>
        /// If set to true, a ".BAK" file will be created with the previously saved file.
        /// </summary>
        public bool SaveBackupFile { get; set; }

        /// <summary>
        /// Path to save the current file
        /// </summary>
        public string SavePath { get; private set; }

        /// <summary>
        /// True if this file is in a read only state and can not be saved to the same file.
        /// </summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>
        /// If set to true, any changes detected by the monitor will notify that s save needs to occur.
        /// </summary>
        public bool IsMonitorEnabled { get; set; } = true;

        /// <summary>
        /// Contains a list of upgrades which will be performed on older versions of files.
        /// Add to this list to include additional upgrades.  Will execute in the order listed.
        /// </summary>
        protected List<PackageUpgrade<TContent>> Upgrades { get; } = new List<PackageUpgrade<TContent>>();

        /// <summary>
        /// True if the file has auto-save turned on.
        /// </summary>
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                Logger.ConditionalTrace("AutoSaveEnabled = {0}.", value);
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
        /// Time this file was initially opened/created.
        /// </summary>
        protected DateTime OpenTime { get; private set; }

        /// <summary>
        /// Path to the auto-save file for this file.
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
        /// If set to true and a file opened is set on a previous version than specified in CurrentVersion,
        /// A copy of all the files in the ProgramName directory is copied into a backup directory named
        /// ProgramName-backup-CurrentVersion.
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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                AllowTrailingCommas = true,
            };

        }

        static Package()
        {
            FileVersion = typeof(Package<TContent>).Assembly.GetName().Version;
        }

        /// <summary>
        /// Method called when opening a file and to run the program specific opening code.
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
                Logger.Error(e, "Unable to parse content.json file.");
                Content = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Method called when saving.  By default, will save the Content file to contents.json
        /// </summary>
        protected virtual async Task OnSave()
        {
            try
            {
                await WriteJson("content.json", Content);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Unable to write content.json file.");
            }
        }

        protected abstract string OnTempFilePathRequest(string fileName);

        /// <summary>
        /// Gets a file inside this zip file.  Must close after usage.
        /// </summary>
        /// <param name="path">Path to the file inside the zip.  Case sensitive.</param>
        /// <returns>Stream on existing file.  Null otherwise.</returns>
        protected internal Stream GetStream(string path)
        {
            var file = _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/" + path);
            return file?.Open();
        }

        /// <summary>
        /// Gets a file inside this zip file.  Must close after usage.
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
        /// Open a JSON file inside the current application directory.
        /// </summary>
        /// <typeparam name="T">Type of JSON file to convert into.</typeparam>
        /// <param name="path">Path to open.  This is a sub-directory of the program name.</param>
        /// <returns>Decoded JSON object.</returns>
        protected internal async ValueTask<T> ReadJson<T>(string path)
        {
            await using var stream = GetStream(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonSerializerOptions);
        }

        /// <summary>
        /// Open a JSON file inside the current application directory.
        /// </summary>
        /// <param name="path">Path to open.  This is a sub-directory of the program name.</param>
        /// <returns>Decoded JSON object.</returns>
        protected internal Task<JsonDocument> ReadJsonDocument(string path)
        {
            using var stream = GetStream(path);
            return JsonDocument.ParseAsync(stream);
        }

        /// <summary>
        /// Returns all the file paths to files inside a directory.
        /// </summary>
        /// <param name="path">Base path of the directory to search inside.</param>
        /// <returns>All paths to the files contained inside a directory.</returns>
        protected internal string[] DirectoryContents(string path)
        {
            var baseDir = _appName + "/";
            return _openArchive.Entries
                .Where(f => f.FullName.StartsWith(baseDir + path))
                .Select(e => e.FullName.Replace(baseDir, ""))
                .ToArray();
        }

        /// <summary>
        /// Saves a type to a JSON file.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="json"></param>
        protected internal async Task WriteJson<T>(string path, T json)
        {
            await using var entityStream = CreateEntityStream(path, true);
            await JsonSerializer.SerializeAsync(entityStream, json, JsonSerializerOptions);
        }

        /// <summary>
        /// Saves a string of text into the specified path.
        /// </summary>
        /// <param name="path">Path to save.  This is a sub-directory of the program name.</param>
        /// <param name="text">String of data to save.</param>
        protected internal async Task WriteString(string path, string text)
        {
            await using var entityStream = CreateEntityStream(path, true);
            await using var writer = new StreamWriter(entityStream);

            await writer.WriteAsync(text);
            await writer.FlushAsync();
        }

        /// <summary>
        /// Saves a stream of data into the current application directory.
        /// </summary>
        /// <param name="path">Path to save.  This is a sub-directory of the program name.</param>
        /// <param name="stream">Stream of data to save.</param>
        protected internal async Task WriteStream(string path, Stream stream)
        {
            await using var entityStream = CreateEntityStream(path, true);
            await stream.CopyToAsync(entityStream);
        }

        /// <summary>
        /// Saves a stream of data into the current application directory.
        /// </summary>
        /// <param name="path">Path to save.  This is a sub-directory of the program name.</param>
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
        /// <param name="path">Path to the entity.  Note, this prefixes the program name to the path.</param>
        /// <param name="prefixProgramName">
        /// Set to true to have the application name be prefixed to all the passed paths.
        /// </param>
        /// <param name="compressionLevel">Compression level for the entity</param>
        /// <returns>Stream to the entity.  Must be closed.</returns>
        private Stream CreateEntityStream(string path,
            bool prefixProgramName,
            CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            if (_saveFileList == null)
                throw new Exception("Can not access save functions while outside of a OnSave call");

            var entityPath = prefixProgramName
                ? _appName + "/" + path
                : path;

            var entity = _saveArchive.CreateEntry(entityPath, compressionLevel);
            _saveFileList.Add(entity.FullName);

            return entity.Open();
        }

        /// <summary>
        /// Opens a file into the current instance.
        /// </summary>
        /// <param name="path">File path to open.</param>
        /// <param name="openReadOnly">
        /// Specifies that the file will be open in read-only mode and can not be saved back
        /// to the source file.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for opening.</param>
        /// <returns>True if opening was successful, otherwise false.</returns>
        public async Task<PackageOpenResult> Open(
            string path,
            bool openReadOnly = false, 
            CancellationToken cancellationToken = default)
        {
            if (_openFileStream != null)
                throw new InvalidOperationException("Can not open file when file is already open");

            await _fileOperationSemaphore.WaitAsync(cancellationToken);

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
                    // Copy the file into memory for use.
                    // Retain the file lock to ensure the file does not get changed.
                    // Open the file in read-only mode.
                    _openFileStream = new FileStream(
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
                    Logger.Error(e, "Unknown error while opening file.");
                    return returnValue = new PackageOpenResult(PackageOpenResultType.UnknownFailure, e);
                }

                // Read the entire contents to memory for usage in read-only mode.
                Stream openFileStreamCopy = null;
                if (openReadOnly)
                {
                    openFileStreamCopy = new MemoryStream((int) _openFileStream.Length);
                    await _openFileStream.CopyToAsync(openFileStreamCopy, cancellationToken);
                    _openFileStream.Close();
                    _openFileStream = null;
                }

                try
                {
                    _openArchive = new ZipArchive(_openFileStream ?? openFileStreamCopy, ZipArchiveMode.Update, true);

                    // Read the version information in the application directory.
                    var versionEntity = _openArchive.Entries.First(f => f.FullName == _appName + "/version");
                    using var reader = new StreamReader(versionEntity.Open());
                    OpenVersion = new Version(await reader.ReadToEndAsync());

                    // Don't allow opening of newer files on older applications.
                    if (_appVersion < OpenVersion)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleVersion);
                }
                catch(Exception e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted, e);
                }



                // Try to open the modified log file.
                // It may not exist in older versions.
                var saveLogEntry =
                    _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/save_log.json");

                if (saveLogEntry != null)
                {
                    try
                    {
                        await using var stream = saveLogEntry.Open();
                        _saveLog = 
                            await JsonSerializer.DeserializeAsync<List<SaveLogItem>>(stream, null, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Could not parse save log.");
                        _saveLog = new List<SaveLogItem>();
                    }
                }

                // Perform upgrades at this time.
                if (_appVersion > OpenVersion)
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
                            var newName = _appName + $"-backup-{OpenVersion}/{entry.FullName}";

                            var saveEntry = _openArchive.CreateEntry(newName);

                            // Copy the last write times to be accurate.
                            saveEntry.LastWriteTime = entry.LastWriteTime;

                            // Copy the streams.
                            await using var openedArchiveStream = entry.Open();
                            await using var saveEntryStream = saveEntry.Open();
                            await openedArchiveStream.CopyToAsync(saveEntryStream, cancellationToken);
                        }
                    }

                    // Loop through each available upgrade.
                    foreach (var upgrade in Upgrades.Where(upgrade => upgrade.Version > OpenVersion))
                    {
                        try
                        {
                            // Attempt to perform the upgrade
                            if (!await upgrade.Upgrade(this, _openArchive))
                            {
                                // Upgrade soft failed, log it and notify the opener.
                                Logger.Error($"Unable to perform upgrade of file {path} to version {upgrade.Version}.");
                                return returnValue = new PackageOpenResult(PackageOpenResultType.UpgradeFailure);
                            }
                        }
                        catch (Exception e)
                        {
                            // Upgrade hard failed.
                            Logger.Error(e, $"Unable to perform upgrade of file {path} to version {upgrade.Version}.");
                            return returnValue = new PackageOpenResult(PackageOpenResultType.UpgradeFailure);
                        }

                    }

                    // Since we did perform an upgrade, set set that the file has been changed.
                    IsDataModified = true;
                }

                // If we are not in read-only mode and we are using lock files, create a lock file
                // and if it failed, stop opening.
                if (!openReadOnly && _useLockFile && !await SetLockFile())
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Locked);
                }

                OpenTime = DateTime.Now;
                AutoSavePath = null;

                return returnValue = await OnOpen(OpenVersion != _appVersion)
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

                _fileOperationSemaphore.Release();
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
        /// IF the file can be deleted, create a new lock file.
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
        /// Saves the current file to the specified path.
        /// </summary>
        /// <param name="path">Path to output the file.</param>
        public async Task<PackageSaveResult> Save(string path)
        {
            // Check the path if we are not auto-saving.
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            Logger.ConditionalTrace("Requesting _fileOperationSemaphore");
            // Ensure we are not already saving.
            await _fileOperationSemaphore.WaitAsync();

            Logger.ConditionalTrace("Acquired _fileOperationSemaphore");

            _saveFileList = new List<string>();
            SavePath = path;

            // If the file is new and not saved yet, a lock file has not been created.
            if (_lockFile == null)
                _lockFilePath = path + ".lock";

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
                
                var result = await SaveInternal(false);

                if (result == PackageSaveResult.Success)
                {
                    // Reset the modified variable if we successfully saved.
                    IsDataModified = false;
                }

                return result;
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
        /// Set to true to bypass saving to the SavePath and instead save the current file to the temp directory.
        /// </param>
        /// <returns>Result of saving</returns>
        private async Task<PackageSaveResult> SaveInternal(bool autoSave)
        {
            Logger.ConditionalTrace("SaveInternal(autoSave:{0})", autoSave);
            PackageSaveResult returnValue = null;
            try
            {
                // TODO: Reuse existing archive when saving from a read-only file.  Prevents duplication of the MS.
                var saveArchiveMemoryStream = new MemoryStream();

                using (_saveArchive = new ZipArchive(saveArchiveMemoryStream, ZipArchiveMode.Create, true))
                {
                    // Write version file
                    await using (var fileVersionStream = CreateEntityStream("file_version", false))
                    {
                        await using var writer = new StreamWriter(fileVersionStream);
                        await writer.WriteAsync(FileVersion.ToString());
                    }

                    await OnSave();

                    // Write version file
                    await WriteString("version", _appVersion.ToString());

                    var log = new SaveLogItem
                    {
                        ComputerName = Environment.MachineName,
                        Username = Environment.UserName,
                        Time = DateTimeOffset.Now,
                        AutoSave = autoSave
                    };

                    // Update the save log.
                    _saveLog.Add(log);

                    // Write the save log.
                    await WriteJson("save_log.json", _saveLog);

                    // If this is an auto save, we do not want to continually add auto save logs.
                    if (autoSave)
                        _saveLog.Remove(log);

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

                // Only save a backup file if we are not performing an auto save.
                if (SaveBackupFile && !autoSave)
                {
                    Logger.ConditionalTrace("Saving backup file.");
                    var bakFile = SavePath + ".bak";
                    try
                    {
                        Logger.ConditionalTrace("Checking for existence of {0} backup file", bakFile);
                        if (File.Exists(bakFile))
                        {
                            Logger.ConditionalTrace("Found backup file");
                            File.SetAttributes(bakFile, FileAttributes.Normal);
                            File.Delete(bakFile);
                            Logger.ConditionalTrace("Deleted backup file");
                        }

                        // Close the lock held on the file.  On initial save, there is nothing to close.
                        _openFileStream?.Close();
                        _openFileStream = null;

                        Logger.ConditionalTrace("Closed _openFileStream.");
                        Logger.ConditionalTrace("Checking for existence of existing save file {0}", SavePath);

                        if (File.Exists(SavePath))
                        {
                            File.Move(SavePath, bakFile);
                            Logger.ConditionalTrace("Found existing save file and renamed to {0}", bakFile);
                        }
                    }
                    catch (Exception e)
                    {
                        return returnValue = new PackageSaveResult(PackageSaveResultType.Failure, e);
                    }
                }

                if (!autoSave)
                {
                    // Create a new file/overwrite existing if a backup was not created.
                    // Retain the lock on the file.
                    if (_openFileStream == null)
                    {
                        // Create a new source file.
                        _openFileStream = new FileStream(SavePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    }
                    else
                    {
                        // Set the file length to 0 to reset for writing.
                        _openFileStream.Position = 0;
                        _openFileStream.SetLength(0);
                    }
                }

                if (autoSave)
                {
                    Logger.ConditionalTrace("Creating AutoSave file {0}.", AutoSavePath);
                }

                var destinationStream = autoSave ? File.Create(AutoSavePath) : _openFileStream;

                if (autoSave)
                {
                    Logger.ConditionalTrace("Created AutoSave file {0}.", AutoSavePath);
                }

                if (destinationStream == null)
                {
                    Logger.ConditionalTrace("Could not create/use destination stream.");
                    return returnValue = new PackageSaveResult(PackageSaveResultType.Failure);
                }

                saveArchiveMemoryStream.Seek(0, SeekOrigin.Begin);
                await saveArchiveMemoryStream.CopyToAsync(destinationStream);

                // If we were auto-saving, close the destination save and return true.
                // We do not want to effect the current state of the rest of the file.
                if (autoSave)
                {
                    destinationStream.Close();
                    Logger.ConditionalTrace("Closed AutoSave destination stream.", AutoSavePath);
                    return returnValue = PackageSaveResult.Success;
                }

                saveArchiveMemoryStream.Seek(0, SeekOrigin.Begin);

                // This implicitly closes the inner stream.
                _openArchive?.Dispose();

                // Save the new zip archive stream to the opened archive stream.
                _openArchive = new ZipArchive(saveArchiveMemoryStream, ZipArchiveMode.Read, true);

                OpenVersion = _appVersion;

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
                // If we are auto-saving, don't change these variables since the file still needs
                // to actually be saved to the destination.
                if (returnValue == PackageSaveResult.Success && autoSave == false)
                {
                    IsDataModified = false;

                    // Since the file was saved, the file is no longer in read-only mode.
                    IsReadOnly = false;
                }

                Logger.ConditionalTrace("InternalSave return: {0}", returnValue);

                _saveFileList = null;

                Logger.ConditionalTrace("Released _fileOperationSemaphore");
                _fileOperationSemaphore.Release();
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
            Logger.ConditionalTrace("ConfigureAutoSave(dueTime:{0}, period:{1})", dueTime, period);

            if (dueTime < -1)
                throw new ArgumentOutOfRangeException(nameof(dueTime), "Value must be -1 or higher.");

            if (period < -1)
                throw new ArgumentOutOfRangeException(nameof(period), "Value must be -1 or higher.");

            _autoSaveDueTime = dueTime;
            _autoSavePeriod = period;

            if (dueTime == 0)
            {
                Logger.ConditionalTrace("Running auto save immediately.");
                // If we call for it to save once immediately, call now.
                await AutoSave(true);

                // If this is a periodical save, push the due time up to the period time since we 
                // just synchronously called the AutoSaveElapsed method.
                if (period > -1)
                {
                    Logger.ConditionalTrace("Forward setting since AutoSave was just called. _autoSaveDueTime = {0}", period);
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
            Logger.ConditionalTrace("AutoSave(synchronous:{0})", synchronous);

            // If the AutoSave path has not been set, set it now.
            AutoSavePath ??= OnTempFilePathRequest(OpenTime.ToString("yyyy-MM-dd-T-HH-mm-ss-fff") + ".sav");

            // Ensure that auto-save is still enabled & that the content has been modified.
            if ((AutoSaveEnabled || synchronous) && IsDataModifiedSinceAutoSave)
            {

                Logger.ConditionalTrace("Requesting _fileOperationSemaphore");
                // Ensure we are not already saving.
                await _fileOperationSemaphore.WaitAsync();
                Logger.ConditionalTrace("Acquired _fileOperationSemaphore");

                _saveFileList = new List<string>();
                var saveResult = await SaveInternal(true);

                if (saveResult == PackageSaveResult.Success)
                {
                    // Set the internal variable to ensure that we do not auto-save more than what is required.
                    IsDataModifiedSinceAutoSave = false;
                }
                else
                {
                    Logger.Error(saveResult.Exception, $"Could not successfully auto-save file {saveResult.SaveResult}");
                }
            }
        }

        private void AutoSaveElapsed(object state)
        {
            Logger.ConditionalTrace("AutoSaveElapsed()");
            _ = AutoSave(false);
        }

        private void SetAutoSaveTimer()
        {
            // Update the current timer if it is running.
            if (_autoSaveEnabled)
            {
                Logger.ConditionalTrace("_autoSaveDueTime = {0}; _autoSavePeriod = {1}", _autoSaveDueTime, _autoSavePeriod);
                _autoSaveTimer.Change(_autoSaveDueTime, _autoSavePeriod);
            }
            else
            {
                Logger.ConditionalTrace("_autoSaveDueTime = -1; _autoSavePeriod = -1");
                _autoSaveTimer.Change(-1, -1);
            }
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
        /// Call when the file has been changed.
        /// </summary>
        protected void DataModified()
        {
            Logger.ConditionalTrace("DataModified()");
            IsDataModified = true;
        }
        
        /// <summary>
        /// Closes the currently open file.
        /// </summary>
        public void Close()
        {
            CloseInternal(true);
        }

        /// <summary>
        /// Closes the currently open file.
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
                    // Usually means that another program has this file opened for reading.
                }
            }

            _openArchive?.Dispose();
            _openArchive = null;
            _openFileStream?.Dispose();
            _openFileStream = null;
            _saveArchive?.Dispose();
            _saveArchive = null;
            _saveFileList = null;
            _lockFile?.Close();
            _lockFile = null;
            _openFileStream?.Close();
            _openFileStream = null;
            _lockFilePath = null;
            _saveLog = null;
            foreach (var registeredListener in _registeredListeners)
                registeredListener.Value.Dispose();

            _registeredListeners.Clear();

            OpenVersion = null;
            SavePath = null;

            IsReadOnly = false;
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