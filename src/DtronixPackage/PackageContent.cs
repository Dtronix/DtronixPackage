using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DtronixPackage;

public abstract class PackageContent : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Override to clear the contents of the current package.
    /// </summary>
    protected internal abstract void Clear(IPackage package);

}