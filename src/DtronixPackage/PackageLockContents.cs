using System;

namespace DtronixPackage;

/// <summary>
/// Contains the data for all lock files.
/// </summary>
public class FileLockContents
{
    /// <summary>
    /// Username of the person who is holding the lock.
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// Date when the file was locked.
    /// </summary>
    public DateTime DateOpened { get; set; }

    public override string ToString()
    {
        return $"DtronixPackageLock: {Username} [{DateOpened}]";
    }
}