using System.Windows.Input;

namespace DtronixPackage.ViewModel
{
    public interface IActionCommand : ICommand
    {
        string GestureText { get; }
        KeyGesture Gesture { get; }
    }
}