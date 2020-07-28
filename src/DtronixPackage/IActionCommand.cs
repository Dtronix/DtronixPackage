using System.Windows.Input;

namespace DtronixPackage
{
    public interface IActionCommand : ICommand
    {
        string GestureText { get; }
        KeyGesture Gesture { get; }
    }
}