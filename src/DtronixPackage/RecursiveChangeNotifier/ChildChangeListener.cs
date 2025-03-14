using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace DtronixPackage.RecursiveChangeNotifier;

internal class ChildChangeListener : ChangeListener
{
    private readonly INotifyPropertyChanged _value;
    private readonly Type _type;
    private readonly Dictionary<string, ChangeListener> _childListeners = new Dictionary<string, ChangeListener>();


    public ChildChangeListener(INotifyPropertyChanged instance)
    {
        _value = instance ?? throw new ArgumentNullException(nameof(instance));
        _type = _value.GetType();

        Subscribe();
    }

    public ChildChangeListener(INotifyPropertyChanged instance, string propertyName)
        : this(instance)
    {
        PropertyName = propertyName;
    }


    private void Subscribe()
    {
        _value.PropertyChanged += Value_PropertyChanged!;

        foreach (var property in _type.GetTypeInfo().DeclaredProperties
                     .Where(p => !p.GetIndexParameters().Any()))
        {
            if (!IsPubliclyReadable(property))
                continue;
            if (!IsNotifier(property.GetValue(obj: _value)!))
                continue;

            ResetChildListener(property.Name);
        }
    }

    private static bool IsPubliclyReadable(PropertyInfo prop) => (prop.GetMethod?.IsPublic ?? false)
                                                                 && !prop.GetMethod.IsStatic;

    private static bool IsNotifier(object value) => value is INotifyCollectionChanged
                                                    || value is INotifyPropertyChanged;

    /// <summary>
    /// Resets known (must exist in children collection) child event handlers
    /// </summary>
    /// <param name="propertyName">Name of known child property</param>
    private void ResetChildListener(string propertyName)
    {
        // Unsubscribe if existing
        if (_childListeners.TryGetValue(propertyName, out var listener)
            && listener != null)
        {
            listener.PropertyChanged -= Child_PropertyChanged;

            // Should unsubscribe all events
            listener.Dispose();
            listener = null;
            _childListeners.Remove(propertyName);
        }

        var property = _type.GetProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException(
                $"Was unable to get '{propertyName}' property information from Type '{_type.Name}'");

        var newValue = property.GetValue(_value, null);

        // Only recreate if there is a new value
        if (newValue != null)
        {
            if (newValue is INotifyCollectionChanged value)
            {
                listener = _childListeners[propertyName] =
                    new CollectionChangeListener(value, propertyName);
            }
            else if (newValue is INotifyPropertyChanged changed)
            {
                listener = _childListeners[propertyName] =
                    new ChildChangeListener(changed, propertyName);
            }

            if (listener == null)
                return;
            listener.PropertyChanged += Child_PropertyChanged;
            listener.CollectionChanged += child_CollectionChanged;
        }
    }


    private void Child_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        RaisePropertyChanged(e.PropertyName);
    }

    private void child_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        RaiseCollectionChanged((INotifyCollectionChanged) sender, args);
    }

    private void Value_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // First, reset child on change, if required...
        ResetChildListener(e.PropertyName);

        // ...then, notify about it
        RaisePropertyChanged(e.PropertyName);
    }

    protected override void RaisePropertyChanged(string propertyName)
    {
        // Special Formatting
        base.RaisePropertyChanged($"{PropertyName}{(PropertyName != null ? "." : null)}{propertyName}");
    }


    /// <summary>
    /// Release all child handlers and self handler
    /// </summary>
    protected override void Unsubscribe()
    {
        _value.PropertyChanged -= Value_PropertyChanged!;

        foreach (var binderKey in _childListeners.Keys.Where(binderKey => _childListeners[binderKey] != null))
        {
            _childListeners[binderKey].Dispose();
        }

        _childListeners.Clear();
    }
}