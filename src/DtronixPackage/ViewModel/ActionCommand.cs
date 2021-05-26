using System;
using System.Globalization;
using System.Windows.Input;

namespace DtronixPackage.ViewModel
{
    internal class ActionCommand : IActionCommand
    {
        private readonly Action _execute;
        private readonly Action<object> _executeParam;
        private bool _canExecute;

        public KeyGesture Gesture { get; }

        public string GestureText => Gesture?.GetDisplayStringForCulture(CultureInfo.CurrentUICulture);

        public event EventHandler CanExecuteChanged;

        public ActionCommand(Action execute, bool canExecute = true, KeyGesture gesture = null)
        {
            _execute = execute;
            _canExecute = canExecute;
            Gesture = gesture;
        }

        public ActionCommand(Action<object> execute, bool canExecute = true, KeyGesture gesture = null)
        {
            _executeParam = execute;
            _canExecute = canExecute;
            Gesture = gesture;
        }

        public bool CanExecute(object parameter)
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

        public void Execute(object parameter)
        {
            _execute?.Invoke();
            _executeParam?.Invoke(parameter);
        }

    }
}
