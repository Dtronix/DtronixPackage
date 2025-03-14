using System;

namespace DtronixPackage.ViewModel;

public interface IActionCommand
{
    internal void SetCanExecute(bool value);
    public bool CanExecute(object? parameter);
    public void Execute(object? parameter);
    public event EventHandler? CanExecuteChanged;
}