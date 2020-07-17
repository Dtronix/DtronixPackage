using System;

namespace DtronixPackage
{
    public class PackageOpenResult
    {
        public static PackageOpenResult Success { get; } = new PackageOpenResult(PackageOpenResultType.Success);

        public FileLockContents LockInfo { get; }

        public PackageOpenResultType OpenFileOpenResultType { get; }
        public Exception Exception { get; }
        public bool IsSuccessful { get; }

        public PackageOpenResult(PackageOpenResultType openFileOpenResultType)
            : this(openFileOpenResultType, null)
        {
        }

        public PackageOpenResult(PackageOpenResultType openFileOpenResultType, Exception exception)
            : this(openFileOpenResultType, exception, null)
        {
        }

        public PackageOpenResult(PackageOpenResultType openFileOpenResultType, Exception exception, FileLockContents lockInfo)
        {
            IsSuccessful = openFileOpenResultType == PackageOpenResultType.Success;
            LockInfo = lockInfo;
            OpenFileOpenResultType = openFileOpenResultType;
            Exception = exception;
        }

        public override string ToString()
        {
            if (this == Success)
                return "Success";

            return $"{OpenFileOpenResultType};Exception: {Exception.Message}";
        }
    }
}