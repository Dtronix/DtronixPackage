using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DtronixPackage;

public interface IPackage : IDisposable
{
    /// <summary>
    /// Called upon closure of a package.
    /// </summary>
    event EventHandler<EventArgs> Closed;

    /// <summary>
    /// Called upon closure of a package.
    /// </summary>
    event EventHandler MonitoredChanged;

    /// <summary>
    /// Contains a log of all the times this package has been saved.
    /// </summary>
    IReadOnlyList<ChangelogEntry> Changelog { get; }

    /// <summary>
    /// Opened package application version.
    /// </summary>
    Version? PackageAppVersion { get; }

    /// <summary>
    /// Current version of the application.
    /// </summary>
    Version? CurrentAppVersion { get; }

    /// <summary>
    /// If set to true, a ".BAK" package will be created with the previously saved package.
    /// </summary>
    bool SaveBackupPackage { get; set; }

    /// <summary>
    /// Path to save the current package.
    /// </summary>
    string? SavePath { get; }

    /// <summary>
    /// True if this package is in a read only state and can not be saved to the same package.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// If set to true, any changes detected by the monitor will notify that s save needs to occur.
    /// </summary>
    bool IsMonitorEnabled { get; set; }

    /// <summary>
    /// Current username for writing to the changelog upon saving.
    /// </summary>
    string Username { get; set; }

    /// <summary>
    /// Current computer name or machine name for writing to the changelog upon saving.
    /// </summary>
    string ComputerName { get; set; }

    /// <summary>
    /// True if the package has auto-save turned on.
    /// </summary>
    bool AutoSaveEnabled { get; set; }

    /// <summary>
    /// True if the data has been modified since the last save.
    /// </summary>
    bool IsContentModified { get; }

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
    Task<PackageOpenResult> Open(
        string path,
        bool openReadOnly = false, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the current package to the specified path.
    /// </summary>
    /// <param name="path">Path to output the package.</param>
    Task<PackageSaveResult> Save(string path);

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
    Task ConfigureAutoSave(int dueTime, int period, bool enable);

    /// <summary>
    /// Whatever changes are made inside the action are not reported back to the monitor.
    /// </summary>
    /// <param name="action">Action to perform.</param>
    void MonitorIgnore(Action action);

    /// <summary>
    /// Closes the currently open package.
    /// </summary>
    void Close();
}