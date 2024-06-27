using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace DtronixPackage.RecursiveChangeNotifier;

internal abstract class ChangeListener : INotifyPropertyChanged, INotifyCollectionChanged, IDisposable
{
    protected string PropertyName;

    protected abstract void Unsubscribe();


    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void RaisePropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    protected virtual void RaiseCollectionChanged(
        INotifyCollectionChanged collection, NotifyCollectionChangedEventArgs args) =>
        CollectionChanged?.Invoke(collection, args);


    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Unsubscribe();
        }
    }

    ~ChangeListener()
    {
        Dispose(false);
    }


    public static ChangeListener Create(INotifyPropertyChanged value)
    {
        return Create(value, null);
    }

    public static ChangeListener Create(INotifyPropertyChanged value, string propertyName)
    {
        if (value is INotifyCollectionChanged trackableCollection)
        {
            return new CollectionChangeListener(trackableCollection, propertyName);
        }

        return value != null ? new ChildChangeListener(value, propertyName) : null;
    }
}