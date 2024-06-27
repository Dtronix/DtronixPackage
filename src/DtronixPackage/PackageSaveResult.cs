using System;

namespace DtronixPackage;

public class PackageSaveResult
{
 

    public static PackageSaveResult Success { get; } = new PackageSaveResult(PackageSaveResultType.Success);
    public static PackageSaveResult Failure { get; } = new PackageSaveResult(PackageSaveResultType.Failure);

    public FileLockContents? LockInfo { get; }

    public PackageSaveResultType SaveResult { get; }
    public Exception? Exception { get; }
    public bool IsSuccessful { get; }

    public PackageSaveResult(PackageSaveResultType openResult)
        : this(openResult, null)
    {
    }

    public PackageSaveResult(PackageSaveResultType openResult, Exception? exception)
        : this(openResult, exception, null)
    {
    }

    public PackageSaveResult(PackageSaveResultType openResult, Exception? exception, FileLockContents? lockInfo)
    {
        IsSuccessful = openResult == PackageSaveResultType.Success;
        LockInfo = lockInfo;
        SaveResult = openResult;
        Exception = exception;
    }

    public override string ToString()
    {
        return $"Result: {SaveResult}; Lock: {LockInfo}; Exception: {Exception}";
    }
}