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
                        _openPackageVersion = new Version(0, 0, 0);
                    }
                    else
                    {
                        using var packageVersionReader = new StreamReader(packageVersionEntry.Open());
                        _openPackageVersion = new Version(await packageVersionReader.ReadToEndAsync());
                    }

                    // Don't allow opening if the package file was saved with a more recent version of the packager.
                    if (PackageVersion < _openPackageVersion)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatiblePackageVersion, PackageVersion);

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
                        Version = new Version(await appVersionReader.ReadToEndAsync());

                    // Don't allow opening of newer packages on older applications.
                    if (AppVersion < Version)
                        return returnValue = new PackageOpenResult(PackageOpenResultType.IncompatibleVersion, Version);
                }
                catch (Exception e)
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Corrupted, e, Version);
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
                    if (upgradeResult != null)
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
                if (AppVersion > Version)
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
                    if (upgradeResult != null)
                        return returnValue = upgradeResult;
                }

                // If we are not in read-only mode and we are using lock files, create a lock file
                // and if it failed, stop opening.
                if (!openReadOnly && _useLockFile && !await SetLockFile())
                {
                    return returnValue = new PackageOpenResult(PackageOpenResultType.Locked, Version);
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
                    : new PackageOpenResult(PackageOpenResultType.ReadingFailure, Version);

            }
            catch (Exception e)
            {
                return returnValue = new PackageOpenResult(PackageOpenResultType.UnknownFailure, e, Version);
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


        private async Task<PackageOpenResult> ApplyUpgrades(
            IList<PackageUpgrade> upgrades,
            bool packageUpgrade,
            Version compareVersion)
        {
            var currentVersion = packageUpgrade ? _openPackageVersion : Version;

            foreach (var upgrade in upgrades.Where(upgrade => upgrade.DependentPackageVersion > compareVersion))
            {
                try
                {
                    // Attempt to perform the upgrade
                    if (!await upgrade.Upgrade(_openArchive))
                    {
                        // Upgrade soft failed, log it and notify the opener.
                        Logger?.Error($"Unable to perform{(packageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.DependentPackageVersion}.");
                        return new PackageOpenResult(PackageOpenResultType.UpgradeFailure, Version);
                    }

                    _changelog.Add(new ChangelogEntry(packageUpgrade
                        ? ChangelogEntryType.PackageUpgrade
                        : ChangelogEntryType.ApplicationUpgrade,
                        Username,
                        ComputerName,
                        CurrentDateTimeOffset)
                    {
                        Note = packageUpgrade
                            ? $"Package upgrade from {currentVersion} to {upgrade.DependentPackageVersion}"
                            : $"Application upgrade from {currentVersion} to {upgrade.DependentPackageVersion}"
                    });

                    currentVersion = upgrade.DependentPackageVersion;
                }
                catch (Exception e)
                {
                    // Upgrade hard failed.
                    Logger?.Error(e, $"Unable to perform{(packageUpgrade ? " package" : " application")} upgrade of package to version {upgrade.DependentPackageVersion}.");
                    return new PackageOpenResult(PackageOpenResultType.UpgradeFailure, e, Version);
                }

                // Since we did perform an upgrade, set set that the package has been changed.
                IsContentModified = true;
            }

            return null;
        }
    }
}
