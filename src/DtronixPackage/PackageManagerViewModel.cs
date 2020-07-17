using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace DtronixPackage
{
    public class FileManagerViewModel<TFile, TFileContent> : INotifyPropertyChanged
        where TFile : Package<TFileContent>, new()
        where TFileContent : FileContent, new()
    {
        private readonly string _appName;
        private readonly Version _appVersion;
        public event PropertyChangedEventHandler PropertyChanged;
        private TFile _file;
        private string _windowTitle;
        private bool _addedModifiedText;
        
        private Dictionary<Window, KeyBinding> _attachedBindings = new Dictionary<Window, KeyBinding>();
        private readonly KeyBinding _saveBinding;
        private readonly KeyBinding _saveAsBinding;
        private readonly KeyBinding _openBinding;
        private readonly KeyBinding _newBinding;

        /// <summary>
        /// The currently managed file by the Manager.
        /// </summary>
        public TFile File {
            get => _file;
            protected set
            {
                if (_file != null)
                    _file.MonitoredChanged -= FileOnMonitoredChanged;

                _file = value;

                if (_file != null)
                    _file.MonitoredChanged += FileOnMonitoredChanged;

                _saveActionCommand.SetCanExecute(_file != null);
                _saveAsActionCommand.SetCanExecute(_file != null);
                _closeActionCommand.SetCanExecute(_file != null);

                OnPropertyChanged();

                FileChanged?.Invoke(this, new FileEventArgs<TFile>(value));
            }
        }

        public event EventHandler<FileEventArgs<TFile>> Created;
        public event EventHandler Closed;
        public event EventHandler<FileEventArgs<TFile>> Opened;
        public event EventHandler<FileEventArgs<TFile>> FileChanged;

        public event EventHandler<PackageMessageEventArgs> ShowMessage; 

        private readonly ActionCommand _saveActionCommand;
        private readonly ActionCommand _saveAsActionCommand;
        private readonly ActionCommand _openActionCommand;
        private readonly ActionCommand _closeActionCommand;
        private readonly ActionCommand _newActionCommand;

        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand NewCommand { get; }

        public string FileFilter { get; set; }
        public string DefaultFilename { get; set; }

        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }

        public bool IsReadOnly => File?.IsReadOnly == true;

        public FileManagerViewModel(string appName, Version appVersion)
        {
            _appName = appName;
            _appVersion = appVersion;
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

        public async void OnWidowClosing(object sender, CancelEventArgs e)
        {
            if (File == null || e.Cancel)
                return;

            // See if there are changes which need to be saved.
            if (File.IsDataModified)
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
            if (File?.IsDataModified == true)
            {
                if (await AskSave() != MessageBoxResult.Yes)
                    return;
            }

            var file = new TFile();

            Created?.Invoke(this, new FileEventArgs<TFile>(file));

            File = file;

            // If we can not save upon creating the file, don't do anything.
            if (!await Save())
            {
                File = null;
            }

            StatusChange();
        }

        private async void OpenCommand_Execute()
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

            if (result != true)
                return;

            await Open(openFile.FileName, openFile.ReadOnlyChecked);
        }

        /// <summary>
        /// Implements the execution of <see cref="CloseCommand" />
        /// </summary>
        private async void CloseCommand_Execute()
        {
            if (File?.IsDataModified == true)
            {
                if (await AskSave() == MessageBoxResult.Cancel)
                    return;
            }

            Close();

        }

        private void Close()
        {
            File?.Close();
            File = null;
            Closed?.Invoke(this, EventArgs.Empty);

            StatusChange();
        }

        public async Task<bool> Open(string path, bool forceReadOnly)
        {
            // If there is already a file open, ask if you want to save the changes before opening another one.
            if (File != null)
            {
                if (await AskSave() == MessageBoxResult.Cancel)
                    return false;

                Close();
            }

            var openFile = new TFile();
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
                File = openFile;
                StatusChange();
                Opened?.Invoke(this, new FileEventArgs<TFile>(File));
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
            if (File == null)
                return false;

            // File is not saved and Read-only considerations.
            if (string.IsNullOrEmpty(File.SavePath)
                && !System.IO.File.Exists(File.SavePath) || File.IsReadOnly)
            {
                return await SaveAs();
            }

            var result = await File.Save(File.SavePath);
            StatusChange();
            
            return result == PackageSaveResult.Success;
        }

        public async Task<bool> SaveAs()
        {
            var saveFile = new SaveFileDialog
            {
                Filter = FileFilter,
                Title = "Save As",
                CheckPathExists = true,
                FileName = DefaultFilename
            };

            if (saveFile.ShowDialog() == true)
            {
                var result = await File.Save(saveFile.FileName);

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

        private void StatusChange()
        {
            _addedModifiedText = false;
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (File == null)
                {
                    WindowTitle = _appName;
                }
                else if (File.SavePath != null && File.IsReadOnly)
                {
                    WindowTitle = $"🔒 {_appName} - {File.SavePath} - Read Only";
                }
                else if (File.SavePath != null)
                {
                    WindowTitle = $"{_appName} - {File.SavePath}";
                }

                // Add modified to the end if there are changes.
                if(File?.IsDataModified == true) 
                    WindowTitle += " (Modified)";

                OnPropertyChanged(nameof(IsReadOnly));
            });
        }

        private void FileOnMonitoredChanged(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => {          
                // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
                _saveActionCommand.SetCanExecute(File?.IsDataModified == true);
            });

            if (_addedModifiedText || !File.IsDataModified)
                return;

            _addedModifiedText = true;
            Application.Current.Dispatcher.Invoke(() =>
            {
                WindowTitle += " (Modified)"; 

                // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
                _saveActionCommand.SetCanExecute(File?.IsDataModified == true);
            });
        }
    }
}