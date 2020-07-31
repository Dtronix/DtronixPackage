using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DtronixPackage.ViewModel
{
    public abstract class PackageManagerViewModel<TPackage, TPackageContent> : INotifyPropertyChanged
        where TPackage : Package<TPackageContent>, new()
        where TPackageContent : PackageContent, new()
    {
        private readonly string _appName;
        public event PropertyChangedEventHandler PropertyChanged;
        private TPackage _package;
        private string _windowTitle;
        private bool _addedModifiedText;
        
        private Dictionary<Window, KeyBinding> _attachedBindings = new Dictionary<Window, KeyBinding>();
        private readonly KeyBinding _saveBinding;
        private readonly KeyBinding _saveAsBinding;
        private readonly KeyBinding _openBinding;
        private readonly KeyBinding _newBinding;

        private Dispatcher _appDispatcher;

        /// <summary>
        /// The currently managed file by the Manager.
        /// </summary>
        public TPackage Package {
            get => _package;
            protected set
            {
                if (_package != null)
                    _package.MonitoredChanged -= PackageOnMonitoredChanged;

                _package = value;

                if (_package != null)
                    _package.MonitoredChanged += PackageOnMonitoredChanged;

                _saveActionCommand.SetCanExecute(_package != null);
                _saveAsActionCommand.SetCanExecute(_package != null);
                _closeActionCommand.SetCanExecute(_package != null);

                OnPropertyChanged();

                FileChanged?.Invoke(this, new PackageEventArgs<TPackage>(value));
            }
        }

        public event EventHandler<PackageEventArgs<TPackage>> Created;
        public event EventHandler Closed;
        public event EventHandler<PackageEventArgs<TPackage>> Opened;
        public event EventHandler<PackageEventArgs<TPackage>> FileChanged;

        public event EventHandler<PackageMessageEventArgs> ShowMessage; 

        private readonly ActionCommand _saveActionCommand;
        private readonly ActionCommand _saveAsActionCommand;
        private readonly ActionCommand _openActionCommand;
        private readonly ActionCommand _closeActionCommand;
        private readonly ActionCommand _newActionCommand;

        public IActionCommand SaveCommand { get; }
        public IActionCommand SaveAsCommand { get; }
        public IActionCommand OpenCommand { get; }
        public IActionCommand CloseCommand { get; }
        public IActionCommand NewCommand { get; }

        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }

        public bool IsReadOnly => Package?.IsReadOnly == true;

        protected PackageManagerViewModel(string appName)
        {
            _appName = appName;
            WindowTitle = appName;
            SaveCommand =_saveActionCommand 
                = new ActionCommand(SaveCommand_Execute, false, new KeyGesture(Key.S, ModifierKeys.Control));

            SaveAsCommand = _saveAsActionCommand 
                = new ActionCommand(SaveAsCommand_Execute, false, 
                    new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));

            OpenCommand = _openActionCommand 
                = new ActionCommand(OpenCommand_Execute, true, new KeyGesture(Key.O, ModifierKeys.Control));

            CloseCommand = _closeActionCommand = new ActionCommand(CloseCommand_Execute, false);
            NewCommand = _newActionCommand 
                = new ActionCommand(NewCommand_Execute, true, new KeyGesture(Key.N, ModifierKeys.Control));

            _saveBinding = new KeyBinding(SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control));
            _saveAsBinding = new KeyBinding(SaveAsCommand, 
                new KeyGesture(Key.S, ModifierKeys.Control | ModifierKeys.Shift));

            _openBinding = new KeyBinding(OpenCommand, new KeyGesture(Key.O, ModifierKeys.Control));
            _newBinding = new KeyBinding(NewCommand, new KeyGesture(Key.N, ModifierKeys.Control));

            // Get the current dispatcher.  Can be null.
            _appDispatcher = Application.Current?.Dispatcher;
        }


        /// <summary>
        /// Called when browsing for a package to open.  Normally paired with OpenFileDialog
        /// </summary>
        /// <remarks>Normally paired with SaveFileDialog</remarks>
        /// <param name="path">Path of the package to open.  Must exist.</param>
        /// <param name="openReadOnly">Set to true to open the package in a read-only state; False to open normally.</param>
        /// <returns>True on successful opening of package. False to cancel the opening process.</returns>
        protected abstract bool BrowseOpenFile(out string path, out bool openReadOnly);

        /// <summary>
        /// Called when browsing for destination to save a package.
        /// </summary>
        /// <remarks>Normally paired with SaveFileDialog</remarks>
        /// <param name="path">Destination path for the package to save.</param>
        /// <returns>True on successful selection of package destination. False to cancel the saving process.</returns>
        protected abstract bool BrowseSaveFile(out string path);

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

        public async void OnWidowClosing(object sender, CancelEventArgs e)
        {
            if (Package == null || e.Cancel)
                return;

            // See if there are changes which need to be saved.
            if (Package.IsDataModified)
            {
                var askSaveResult = await AskSave();

                // If they said anything but yes or no, then cancel the closing
                if (askSaveResult != MessageBoxResult.Yes && askSaveResult != MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            Close();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Implements the execution of <see cref="SaveCommand" />
        /// </summary>
        private async void SaveCommand_Execute()
        {
            await Save();
        }

        /// <summary>
        /// Implements the execution of <see cref="SaveAsCommand" />
        /// </summary>
        private async void SaveAsCommand_Execute()
        {
            await SaveAs();
        }

        private async void NewCommand_Execute()
        {
            if (Package?.IsDataModified == true)
            {
                if (await AskSave() != MessageBoxResult.Yes)
                    return;
            }

            var file = new TPackage();

            Created?.Invoke(this, new PackageEventArgs<TPackage>(file));

            Package = file;

            // If we can not save upon creating the file, don't do anything.
            if (!await Save())
            {
                Package = null;
            }

            StatusChange();
        }

        private async void OpenCommand_Execute()
        {
            var result = BrowseOpenFile(out var path, out var readOnly);

            if (result != true)
                return;

            await Open(path, readOnly);
        }

        /// <summary>
        /// Implements the execution of <see cref="CloseCommand" />
        /// </summary>
        private async void CloseCommand_Execute()
        {
            if (Package?.IsDataModified == true)
            {
                if (await AskSave() == MessageBoxResult.Cancel)
                    return;
            }

            Close();

        }

        private void Close()
        {
            Package?.Close();
            Package = null;
            Closed?.Invoke(this, EventArgs.Empty);

            StatusChange();
        }

        public async Task<bool> Open(string path, bool forceReadOnly)
        {
            // If there is already a file open, ask if you want to save the changes before opening another one.
            if (Package != null)
            {
                if (await AskSave() == MessageBoxResult.Cancel)
                    return false;

                Close();
            }

            var openFile = new TPackage();
            var result = await openFile.Open(path, forceReadOnly);
            
            // If the file is locked, give the option to open read-only.
            if (result.IsSuccessful == false)
            {
                string readOnlyText;
                if (result.LockInfo != null)
                    readOnlyText = $"{path} is currently opened by {result.LockInfo.Username} on {result.LockInfo.DateOpened:F}.";
                else
                    readOnlyText = $"{path} Could not open the file for editing because it is in use or is read-only.";

                // Try opening read-only?
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.YesNo,
                    readOnlyText + "\nWould you like to open the file read-only?",
                    "Open Confirmation",
                    MessageBoxImage.Exclamation);
                ShowMessage?.Invoke(this, message);

                // If the user did not agree to opening read-only, there is nothing to do.
                if (message.Result != MessageBoxResult.Yes)
                    return false;

                result = await openFile.Open(path, true);
            }

            if (result.IsSuccessful)
            {
                Package = openFile;
                StatusChange();
                Opened?.Invoke(this, new PackageEventArgs<TPackage>(Package));
                return true;
            }

            ShowMessage?.Invoke(this, new PackageMessageEventArgs(
                PackageMessageEventArgs.MessageType.OK,
                $"{path} Could not open file because it is currently in use or is read-only.",
                "Can't open file",
                MessageBoxImage.Exclamation));

            StatusChange();
            return false;
        }
        
        public async Task<MessageBoxResult> AskSave()
        {
            var message = new PackageMessageEventArgs(
                PackageMessageEventArgs.MessageType.YesNoCancel,
                "Do you want to save the open file?",
                "Save");
            ShowMessage?.Invoke(this, message);

            if (message.Result != MessageBoxResult.Yes) 
                return message.Result;

            // If the save was not successful, cancel.
            if(!await Save())
                return MessageBoxResult.Cancel;

            return message.Result;
        }

        public async Task<bool> Save()
        {
            if (Package == null)
                return false;

            // File is not saved and Read-only considerations.
            if (string.IsNullOrEmpty(Package.SavePath)
                && !System.IO.File.Exists(Package.SavePath) || Package.IsReadOnly)
            {
                return await SaveAs();
            }

            var result = await Package.Save(Package.SavePath);
            StatusChange();
            
            return result == PackageSaveResult.Success;
        }

        public async Task<bool> SaveAs()
        {
            if (BrowseSaveFile(out var path))
            {
                var result = await Package.Save(path);

                switch (result.SaveResult)
                {
                    case PackageSaveResultType.Locked:
                        ShowMessage?.Invoke(this, new PackageMessageEventArgs(
                            PackageMessageEventArgs.MessageType.OK,
                            "Selected file is locked by another application.  Please select another location.",
                            "Saving Error",
                            MessageBoxImage.Error));
                        return await SaveAs();

                    case PackageSaveResultType.Failure:
                        ShowMessage?.Invoke(this, new PackageMessageEventArgs(
                            PackageMessageEventArgs.MessageType.OK,
                            "File could not be saved at specified location.  Please select another location.",
                            "Saving Error",
                            MessageBoxImage.Error));
                        return await SaveAs();
                }

                StatusChange();
                return true;
            }

            StatusChange();
            return false;
        }

        private void InvokeOnDispatcher(Action action)
        {
            if (_appDispatcher == null)
            {
                action?.Invoke();
                return;
            }

            _appDispatcher.Invoke(action);
        }

        private void StatusChange()
        {
            _addedModifiedText = false;
            InvokeOnDispatcher(() =>
            {
                if (Package == null)
                {
                    WindowTitle = _appName;
                }
                else if (Package.SavePath != null && Package.IsReadOnly)
                {
                    WindowTitle = $"🔒 {_appName} - {Package.SavePath} - Read Only";
                }
                else if (Package.SavePath != null)
                {
                    WindowTitle = $"{_appName} - {Package.SavePath}";
                }

                // Add modified to the end if there are changes.
                if(Package?.IsDataModified == true) 
                    WindowTitle += " (Modified)";

                OnPropertyChanged(nameof(IsReadOnly));
            });
        }

        private void PackageOnMonitoredChanged(object sender, EventArgs e)
        {
            InvokeOnDispatcher(() => {          
                // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
                _saveActionCommand.SetCanExecute(Package?.IsDataModified == true);
            });

            if (_addedModifiedText || !Package.IsDataModified)
                return;

            _addedModifiedText = true;
            InvokeOnDispatcher(() =>
            {
                WindowTitle += " (Modified)"; 

                // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
                _saveActionCommand.SetCanExecute(Package?.IsDataModified == true);
            });
        }
    }
}