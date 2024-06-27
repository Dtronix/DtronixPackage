using System;

namespace DtronixPackage.ViewModel;

public class PackageEventArgs<T> : EventArgs
{
    public T? Package { get; set; }

    public PackageEventArgs(T? package)
    {
        Package = package;
    }
}