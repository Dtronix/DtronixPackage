using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
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
    public abstract partial class Package<TContent> : IPackage 
        where TContent : PackageContent, new()
    {
        private readonly string _appName;
        private readonly bool _preserveUpgrade;
        private readonly bool _useLockFile;
        private ZipArchive _openArchive;
        private FileStream _lockFile;
        private FileStream _openPackageStream;

        private string _lockFilePath;
        private readonly SemaphoreSlim _packageOperationSemaphore = new(1, 1);
        private readonly List<ChangelogEntry> _changelog = new();
        private readonly Timer _autoSaveTimer;
        private bool _autoSaveEnabled;
        private readonly Dictionary<object, ChangeListener> _monitorListeners = new();
        private int _autoSavePeriod = 60 * 1000;
        private int _autoSaveDueTime = 60 * 1000;
        private bool _disposed;
        private bool _isContentModified;
        private Version _openPkgVersion;

        protected static ILogger Logger;

        internal static readonly Version CurrentPkgVersion 
            = typeof(IPackage).Assembly.GetName().Version;

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
        /// Opened package application version.
        /// </summary>
        public Version PackageAppVersion { get; private set; }

        /// <summary>
        /// Current version of the application.
        /// </summary>
        public Version CurrentAppVersion { get; }
        
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
        /// Current username for writing to the changelog upon saving.
        /// </summary>
        public string Username { get; set; } = Environment.UserName;

        /// <summary>
        /// Current computer name or machine name for writing to the changelog upon saving.
        /// </summary>
        public string ComputerName { get; set; } = Environment.MachineName;

        /// <summary>
        /// Contains a list of upgrades which will be performed on older versions of packages.
        /// Add to this list to include additional upgrades.  Will execute in the order listed.
        /// </summary>
        protected List<PackageUpgrade> Upgrades { get; } = new();

        /// <summary>
        /// Internal calculated property used to get the current time.
        /// Used for testing.
        /// </summary>
        internal virtual DateTimeOffset CurrentDateTimeOffset => DateTimeOffset.Now;

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
        public bool IsContentModified
        {
            get => _isContentModified;
            internal set
            {
                _isContentModified = value;
                IsDataModifiedSinceAutoSave = value;

                // Only invoke the MonitorChanged event if there are changes.
                if(value)
                    MonitoredChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Stored content of the package.
        /// </summary>
        public TContent Content { get; }

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
        /// <param name="currentAppVersion">
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
        protected Package(string appName, Version currentAppVersion, bool preserveUpgrade, bool useLockFile)
        {
            _appName = appName;
            CurrentAppVersion = currentAppVersion;
            _preserveUpgrade = preserveUpgrade;
            _useLockFile = useLockFile;

            // Create a dummy time to allow for 
            OpenTime = DateTime.Now;

            _autoSaveTimer = new Timer(AutoSaveElapsed);

            JsonSerializerOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = null,
                AllowTrailingCommas = true,
            };

            Content = new TContent();
            MonitorRegister(Content);
        }

        /// <summary>
        /// Override with a method to specify a temp file location.
        /// </summary>
        /// <param name="fileName">Filename to include in the temp file path result.</param>
        /// <returns>New temp file path location.</returns>
        protected abstract string OnTempFilePathRequest(string fileName);

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

        protected void MonitorRegister<T>(T obj)
            where T : INotifyPropertyChanged
        {
            if (obj == null)
                return;

            // Do not double register the same object.
            if (_monitorListeners.ContainsKey(obj))
                return;
            
            var listener = ChangeListener.Create(obj);
            listener.CollectionChanged += MonitorListenerOnCollectionChanged;
            listener.PropertyChanged += MonitorListenerOnPropertyChanged;
            _monitorListeners.Add(obj, listener);

        }

        protected void MonitorDeregister<T>(T obj)
            where T : INotifyPropertyChanged
        {
            if (obj == null)
                return;

            // Do nothing if it is not registered.
            if (!_monitorListeners.TryGetValue(obj, out var listener))
                return;

            _monitorListeners.Remove(obj);
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
            IsContentModified = true;
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
            _lockFile?.Close();
            _lockFile = null;
            _openPackageStream?.Close();
            _openPackageStream = null;
            _lockFilePath = null;
            _changelog.Clear();
            _openPkgVersion = null;
            foreach (var registeredListener in _monitorListeners)
                registeredListener.Value.Dispose();

            _monitorListeners.Clear();
            Content.Clear(this);
            MonitorRegister(Content);

            PackageAppVersion = null;
            SavePath = null;

            IsReadOnly = false;

            // Disable auto-save only if it is enabled.
            if(AutoSaveEnabled)
                AutoSaveEnabled = false;

            IsContentModified = false;
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