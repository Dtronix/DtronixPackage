using System;

namespace DtronixPackage;

public class PackageOpenResult
{
    public static PackageOpenResult Success { get; } = new PackageOpenResult(PackageOpenResultType.Success);

    public FileLockContents LockInfo { get; }

    public PackageOpenResultType Result { get; }
    public Exception Exception { get; }
    public bool IsSuccessful { get; }
    public Version OpenVersion { get; set; }

    public PackageOpenResult(PackageOpenResultType result)
        : this(result, exception:null)
    {
    }

        
    public PackageOpenResult(PackageOpenResultType result, Version openVersion)
        : this(result, exception:null)
    {
        OpenVersion = openVersion;
    }


    public PackageOpenResult(PackageOpenResultType result, Exception exception)
        : this(result, exception, lockInfo:null)
    {
    }

    public PackageOpenResult(PackageOpenResultType result, Exception exception, Version openVersion)
        : this(result, exception, lockInfo:null)
    {
        OpenVersion = openVersion;
    }

    public PackageOpenResult(PackageOpenResultType result, Exception exception, FileLockContents lockInfo)
    {
        IsSuccessful = result == PackageOpenResultType.Success;
        LockInfo = lockInfo;
        Result = result;
        Exception = exception;
    }

    public override string ToString()
    {
        if (this == Success)
            return "Success";

        return $"{Result};Exception: {Exception?.Message}";
    }
}