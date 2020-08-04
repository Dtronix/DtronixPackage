namespace DtronixPackage
{
    public enum PackageOpenResultType
    {
        Unset,

        /// <summary>
        /// Opening was successful.
        /// </summary>
        Success,

        /// <summary>
        /// Used when the version or required files are not present in the package.
        /// </summary>
        Corrupted,

        /// <summary>
        /// File is currently locked.  Try opening with the ReadOnly parameter.
        /// </summary>
        Locked,

        /// <summary>
        /// Unknown reason for failure.  Review exception.
        /// </summary>
        UnknownFailure,

        /// <summary>
        /// During the OnOpen reading stage, the reader returned false indicating a reading failure.
        /// </summary>
        ReadingFailure,

        /// <summary>
        /// User does not have the proper permissions to access this package.
        /// </summary>
        PermissionFailure,

        /// <summary>
        /// Upgrade was attempted on this package but failed.
        /// </summary>
        UpgradeFailure,

        /// <summary>
        /// Opened package application version is newer than the currently running application.
        /// </summary>
        IncompatibleVersion,

        /// <summary>
        /// Incompatible application means that the opened file is a package file but it was saved with
        /// a different application which is incompatible with this opening application.
        /// </summary>
        IncompatibleApplication,

        /// <summary>
        /// Package path was not found.
        /// </summary>
        FileNotFound
    }
}