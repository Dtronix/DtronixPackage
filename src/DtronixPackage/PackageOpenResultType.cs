namespace DtronixPackage
{
    public enum PackageOpenResultType
    {
        Unset,
        Success,
        Corrupted,
        Locked,
        UnknownFailure,
        ReadingFailure,
        PermissionFailure,
        UpgradeFailure,
        IncompatibleVersion,
        FileNotFound
    }
}