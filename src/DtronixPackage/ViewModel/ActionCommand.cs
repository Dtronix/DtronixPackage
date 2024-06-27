using System;
using System.Globalization;
using System.Windows.Input;

namespace DtronixPackage.ViewModel;

internal class ActionCommand : IActionCommand
{
    private readonly Action _execute;
    private readonly Action<object?> _executeParam;
    private bool _canExecute;

    public event EventHandler CanExecuteChanged;

    public ActionCommand(Action execute, bool canExecute = true)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public ActionCommand(Action<object?> execute, bool canExecute = true)
    {
        _executeParam = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute;
    }

    void IActionCommand.SetCanExecute(bool value)
    {
        if (_canExecute == value)
            return;

        _canExecute = value;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Execute(object? parameter)
    {
        _execute?.Invoke();
        _executeParam?.Invoke(parameter);
    }

}