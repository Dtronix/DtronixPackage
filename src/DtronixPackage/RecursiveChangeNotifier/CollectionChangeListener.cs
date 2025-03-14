using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace DtronixPackage.RecursiveChangeNotifier;

internal class CollectionChangeListener : ChangeListener
{
    private readonly INotifyCollectionChanged _value;
    private readonly Dictionary<INotifyPropertyChanged, ChangeListener> _collectionListeners = new Dictionary<INotifyPropertyChanged, ChangeListener>();
    public INotifyCollectionChanged Value => _value;

    public CollectionChangeListener(INotifyCollectionChanged collection, string propertyName)
    {
        _value = collection;
        PropertyName = propertyName;

        Subscribe();
    }


    private void Subscribe()
    {
        _value.CollectionChanged += Value_CollectionChanged;

        foreach (var item in ((IEnumerable)_value).OfType<INotifyPropertyChanged>())
        {
            ResetChildListener(item);
        }
    }

    private void ResetChildListener(INotifyPropertyChanged item)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        RemoveItem(item);

        // Add new
        var listener = item is INotifyCollectionChanged trackableCollection
            ? (ChangeListener)new CollectionChangeListener(trackableCollection, PropertyName)
            : new ChildChangeListener(item);

        listener.PropertyChanged += Listener_PropertyChanged;
        listener.CollectionChanged += listener_CollectionChanged;
        _collectionListeners.Add(item, listener);
    }

    private void RemoveItem(INotifyPropertyChanged item)
    {
        // Remove old
        if (_collectionListeners.ContainsKey(item))
        {
            _collectionListeners[item].PropertyChanged -= Listener_PropertyChanged;
            _collectionListeners[item].CollectionChanged -= listener_CollectionChanged;

            _collectionListeners[item].Dispose();
            _collectionListeners.Remove(item);
        }
    }


    private void ClearCollection()
    {
        foreach (var key in _collectionListeners.Keys)
        {
            _collectionListeners[key].Dispose();
        }

        _collectionListeners.Clear();
    }


    void Value_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearCollection();
        }
        else
        {
            // Don't care about e.Action, if there are old items, Remove them...
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                    RemoveItem(item);
            }

            // ...add new items as well
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                    ResetChildListener(item);
            }
        }

        Debug.Assert(sender == _value);
        RaiseCollectionChanged(_value, e);
    }


    void Listener_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // ...then, notify about it
        // ReSharper disable once ExplicitCallerInfoArgument
        RaisePropertyChanged($"{PropertyName}{(PropertyName != null ? "[]." : null)}{e.PropertyName}");
    }
    void listener_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        RaiseCollectionChanged((INotifyCollectionChanged)sender, e);
    }


    /// <summary>
    /// Releases all collection item handlers and self handler
    /// </summary>
    protected override void Unsubscribe()
    {
        ClearCollection();

        _value.CollectionChanged -= Value_CollectionChanged!;
    }
}