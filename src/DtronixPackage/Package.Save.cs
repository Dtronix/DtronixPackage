using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.Logging;

namespace DtronixPackage;

public abstract partial class Package<TContent> : IPackage
    where TContent : PackageContent, new()
{

    /// <summary>
    /// Method called when saving.  By default, will save the Content object to contents.json
    /// </summary>
    /// <param name="writer">Package writer.</param>
    protected abstract Task OnWrite(PackageWriter writer);

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

                if (lockExists)
                    return new PackageSaveResult(PackageSaveResultType.Locked, null, lockFile);

                if (!await SetLockFile())
                    return new PackageSaveResult(PackageSaveResultType.Locked);
            }

            return await SaveInternal(false);
        }
        catch (Exception e)
        {
            return new PackageSaveResult(PackageSaveResultType.Failure, e);
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

            using (var archive = new ZipArchive(saveArchiveMemoryStream, ZipArchiveMode.Create, true))
            {
                var writer = new PackageWriter(archive, JsonSerializerOptions, _appName);

                // Write application version file
                await using (var packageVersionStream = writer.CreateEntityStream("version", false))
                {
                    await using var packageVersionStreamWriter = new StreamWriter(packageVersionStream);
                    await packageVersionStreamWriter.WriteAsync(CurrentPkgVersion.ToString());
                }

                await OnWrite(writer);

                // Write package version file
                await writer.Write("version", CurrentAppVersion.ToString());

                var log = new ChangelogEntry
                (autoSave ? ChangelogEntryType.AutoSave : ChangelogEntryType.Save,
                    Username,
                    ComputerName,
                    CurrentDateTimeOffset);

                // Update the save log.
                _changelog.Add(log);

                // Write the save log.
                await writer.WriteJson("changelog.json", _changelog);

                // If this is an auto save, we do not want to continually add auto save logs.
                if (autoSave)
                    _changelog.Remove(log);

                if (_openArchive != null)
                {
                    foreach (var openedArchiveEntry in _openArchive.Entries)
                    {
                        if (writer.FileList.Contains(openedArchiveEntry.FullName))
                            continue;

                        var saveEntry = archive.CreateEntry(openedArchiveEntry.FullName);

                        // Copy the last write times to be accurate.
                        saveEntry.LastWriteTime = openedArchiveEntry.LastWriteTime;

                        // Copy the streams.
                        await using var openedArchiveStream = openedArchiveEntry.Open();
                        await using var saveEntryStream = saveEntry.Open();
                        await openedArchiveStream.CopyToAsync(saveEntryStream);
                    }
                }

            }

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

            PackageAppVersion = CurrentAppVersion;

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
                IsContentModified = false;

                // Since the package was saved, the package is no longer in read-only mode.
                IsReadOnly = false;
            }

            Logger?.ConditionalTrace($"InternalSave return: {returnValue}");
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
    /// <param name="enable">Set to true to enable saving now if configured to run.</param>
    public async Task ConfigureAutoSave(int dueTime, int period, bool enable)
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
        AutoSaveEnabled = enable && (dueTime >= 0 || period >= 0);
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
}