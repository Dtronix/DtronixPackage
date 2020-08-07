using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace DtronixPackage.ViewModel
{
    public abstract class WindowsPackageManagerViewModel<TPackage, TPackageContent> : PackageManagerViewModel<TPackage, TPackageContent>
        where TPackage : Package<TPackageContent>, new()
        where TPackageContent : PackageContent, new()
    {
        private readonly Dictionary<Window, KeyBinding> _attachedBindings = new Dictionary<Window, KeyBinding>();
        private readonly KeyBinding _saveBinding;
        private readonly KeyBinding _saveAsBinding;
        private readonly KeyBinding _openBinding;
        private readonly KeyBinding _newBinding;

        /// <summary>
        /// File filter applied to Open and Save file dialogs
        /// </summary>
        /// <remarks>
        /// Format: "App Name|*.ext"
        /// </remarks>
        public abstract string FileFilter { get; }

        /// <summary>
        /// Default name for the package when saving.
        /// </summary>
        public abstract string DefaultPackageName { get; }


        protected WindowsPackageManagerViewModel(string appName) 
            : base(appName)
        {
            _saveBinding = new KeyBinding(SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control));
            _saveAsBinding = new KeyBinding(SaveAsCommand, 
                new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));

            _openBinding = new KeyBinding(OpenCommand, new KeyGesture(Key.O, ModifierKeys.Control));
            _newBinding = new KeyBinding(NewCommand, new KeyGesture(Key.N, ModifierKeys.Control));
        }

        
        public void AttachInputBindings(
            UIElement uiElement, 
            FileManagerInputBindings bindings = FileManagerInputBindings.All)
        {
            var eleBinds = uiElement.InputBindings;

            if (bindings.HasFlag(FileManagerInputBindings.Save))
            {
                if (!eleBinds.Contains(_saveBinding))
                    eleBinds.Add(_saveBinding);

                if (!eleBinds.Contains(_saveAsBinding))
                    eleBinds.Add(_saveAsBinding);
            }

            if (bindings.HasFlag(FileManagerInputBindings.Creation))
            {
                if (!eleBinds.Contains(_newBinding))
                    eleBinds.Add(_newBinding);
            }

            if (bindings.HasFlag(FileManagerInputBindings.Open))
            {
                if (!eleBinds.Contains(_openBinding))
                    eleBinds.Add(_openBinding);
            }
        }

        public void DetachInputBindings(UIElement uiElement)
        {
            var eleBinds = uiElement.InputBindings;

            if (eleBinds.Contains(_saveBinding))
                eleBinds.Remove(_saveBinding);

            if (eleBinds.Contains(_saveAsBinding))
                eleBinds.Remove(_saveAsBinding);

            if (eleBinds.Contains(_newBinding))
                eleBinds.Remove(_newBinding);

            if (eleBinds.Contains(_openBinding))
                eleBinds.Remove(_openBinding);
        }

        protected override bool BrowseSaveFile(out string path)
        {
            var saveFile = new SaveFileDialog
            {
                Filter = FileFilter,
                Title = "Save As",
                CheckPathExists = true,
                FileName = DefaultPackageName,
            };

            var result = saveFile.ShowDialog();

            path = saveFile.FileName;

            return result == true;
        }

        protected override bool BrowseOpenFile(out string path, out bool openReadOnly)
        {
            var openFile = new OpenFileDialog
            {
                Filter = FileFilter,
                Title = "Open",
                CheckPathExists = true,
                CheckFileExists = true,
                ShowReadOnly = true
            };
            var result = openFile.ShowDialog();

            openReadOnly = openFile.ReadOnlyChecked;
            path = openFile.FileName;

            return result == true;
        }

        public async void OnWidowClosing(object sender, CancelEventArgs e)
        {
            if (Package == null || e.Cancel)
                return;

            // Attempt to close if any package is open.
            if (!await TryClose())
                e.Cancel = true;
        }

    }
}
