using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DtronixPackage.Logging;
using DtronixPackage.Upgrades;

namespace DtronixPackage
{
    public abstract partial class Package<TContent> : IPackage
        where TContent : PackageContent, new()
    {

        /// <summary>
        /// Method called when opening a package file and to run the application specific opening code.
        /// By default, will read content.json and copy the contents to the Content object.
        /// </summary>
        /// <param name="reader">Package reader.</param>
        /// <returns>True of successful opening. False otherwise.</returns>
        protected abstract Task<bool> OnRead(PackageReader reader);

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
            var isMonitorEnabledOriginalValue = IsMonitorEnabled;
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

                    if (lockExists)
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
                        _openPkgVersion = new Version(0, 0, 0);
                    }
                    else
                    {
                        using var packageVersionReader = new StreamReader(packageVersionEntry.Open());
                        _openPkgVersion = new Version(await packageVersionReader.ReadToEndAsync());
                    }

                    // Don't allow opening if the package file was saved with a more recent version of the packager.
                    if (CurrentPkgVersion < _openPkgVersion)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatiblePackageVersion, CurrentPkgVersion);

                    // Read the version information in the application directory.
                    var versionEntity = _openArchive.Entries.FirstOrDefault(f => f.FullName == _appName + "/version");

                    // If the versionEntity is empty, this usually means that this package was not created by the same
                    // application currently opening the package.
                    if (versionEntity == null)
                    {

                        // Check to see if there is a version file in the entire package.  If there is, there is a high
                        // probability that this is a Package file, just used by another application's package system.
                        if (_openArchive.Entries.Any(f => f.FullName.EndsWith("/version")))
                            return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleApplication);
                        else
                            return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted);
                    }

                    using (var appVersionReader = new StreamReader(versionEntity.Open()))
                        PackageAppVersion = new Version(await appVersionReader.ReadToEndAsync());

                    // Don't allow opening of newer packages on older applications.
                    if (CurrentAppVersion < PackageAppVersion)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleVersion, PackageAppVersion);
                }
                catch (Exception e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted, e, PackageAppVersion);
                }

                var upgradeManager = new UpgradeManager(_openPkgVersion, PackageAppVersion);

                // Set to true below if either the package or application versions changed.
                var versionChanged = false;

                // If this package version is older than the current version, add the upgrades to the manager.
                if (CurrentPkgVersion > _openPkgVersion)
                {
                    upgradeManager.Add(new PackageUpgrade_1_1_0());
                    versionChanged = true;
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
                            options: null,
                            cancellationToken);

                        foreach (var changelogItem in logItems)
                            _changelog.Add(changelogItem);

                    }
                    catch (Exception e)
                    {
                        Logger?.Error(e, "Could not read save log.");
                        _changelog.Clear();
                    }
                }

                // Perform upgrades at this time.
                if (CurrentAppVersion > PackageAppVersion)
                {
                    foreach (var packageUpgrade in Upgrades)
                        upgradeManager.Add(packageUpgrade);

                    versionChanged = true;
                }

                // "Renames" existing contents of this application's directory to a backup directory
                if (_preserveUpgrade && versionChanged)
                {
                    var entries = _openArchive.Entries.ToArray();
                    foreach (var entry in entries)
                    {
                        // If there is a version miss-match, save the original files in a modified
                        // directory to retrieve in-case of corruption.
                        if (!entry.FullName.StartsWith(_appName + "/"))
                            continue;

                        // Renames all the sub-files if there was a version mis-match on open.
                        var newName = _appName + $"-backup-{PackageAppVersion}/{entry.FullName}";

                        var saveEntry = _openArchive.CreateEntry(newName);

                        // Copy the last write times to be accurate.
                        saveEntry.LastWriteTime = entry.LastWriteTime;

                        // Copy the streams.
                        await using var openedArchiveStream = entry.Open();
                        await using var saveEntryStream = saveEntry.Open();
                        await openedArchiveStream.CopyToAsync(saveEntryStream, cancellationToken);
                    }
                }

                if (upgradeManager.HasUpgrades)
                {
                    var upgradeResult = await ApplyUpgrades(upgradeManager);

                    // If the result is not null, the upgrade failed.
                    if (upgradeResult != null)
                        return returnValue = upgradeResult;
                }

                // If we are not in read-only mode and we are using lock files, create a lock file
                // and if it failed, stop opening.
                if (!openReadOnly && _useLockFile && !await SetLockFile())
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Locked, PackageAppVersion);
                }

                OpenTime = DateTime.Now;
                AutoSavePath = null;

                var reader = new PackageReader(_openArchive, JsonSerializerOptions, _appName);

                // Disable monitor notifications while reading.
                IsMonitorEnabled = false;

                // Perform the overridden reading operations.
                var openResult = await OnRead(reader);

                return returnValue = openResult
                    ? PackageOpenResult.Success
                    : new PackageOpenResult(PackageOpenResultType.ReadingFailure, PackageAppVersion);

            }
            catch (Exception e)
            {
                return returnValue = new PackageOpenResult(PackageOpenResultType.UnknownFailure, e, PackageAppVersion);
            }
            finally
            {
                // Perform cleanup if the open was not successful.
                // Do not fire the close event since it was not successfully opened in the first place.
                if (returnValue != PackageOpenResult.Success)
                    CloseInternal(false);

                // Restore the original monitor value.
                IsMonitorEnabled = isMonitorEnabledOriginalValue;

                _packageOperationSemaphore.Release();
            }
        }


        private async Task<PackageOpenResult> ApplyUpgrades(IEnumerable<PackageUpgrade> upgrades)
        {
            foreach (var upgrade in upgrades)
            {
                var isPackageUpgrade = upgrade is InternalPackageUpgrade;
                var applicationUpgrade = upgrade as ApplicationPackageUpgrade;
                var currentVersion = isPackageUpgrade ? _openPkgVersion : PackageAppVersion;

                try
                {
                    // Attempt to perform the upgrade
                    if (!await upgrade.Upgrade(_openArchive))
                    {
                        // Upgrade soft failed, log it and notify the opener.
                        Logger?.Error($"Unable to perform{(isPackageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.DependentPackageVersion}.");
                        return new PackageOpenResult(PackageOpenResultType.UpgradeFailure, PackageAppVersion);
                    }

                    _changelog.Add(new ChangelogEntry(isPackageUpgrade
                        ? ChangelogEntryType.PackageUpgrade
                        : ChangelogEntryType.ApplicationUpgrade,
                        Username,
                        ComputerName,
                        CurrentDateTimeOffset)
                    {
                        Note = isPackageUpgrade
                            ? $"Package upgrade from {currentVersion} to {upgrade.DependentPackageVersion}"
                            : $"Application upgrade from {currentVersion} to {applicationUpgrade!.AppVersion}"
                    });
                }
                catch (Exception e)
                {
                    // Upgrade hard failed.
                    Logger?.Error(e, $"Unable to perform{(isPackageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.DependentPackageVersion}.");
                    return new PackageOpenResult(PackageOpenResultType.UpgradeFailure, e, PackageAppVersion);
                }

                // Since we did perform an upgrade, set set that the package has been changed.
                IsContentModified = true;
            }

            return null;
        }
    }
}
