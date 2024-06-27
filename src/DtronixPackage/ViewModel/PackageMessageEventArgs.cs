using System;
using System.Windows;

namespace DtronixPackage.ViewModel;

public class PackageMessageEventArgs : EventArgs
{
    public enum MessageType
    {
        OK,
        YesNo,
        YesNoCancel,
    }
    public MessageType Type { get; set; }
    public MessageBoxResult Result { get; set; }

    public string MessageBoxText { get; set; }
    public string Caption { get; set; }

    public PackageMessageEventArgs(MessageType type, string messageBoxText, string caption)
    {
        Type = type;
        MessageBoxText = messageBoxText;
        Caption = caption;
    }
}