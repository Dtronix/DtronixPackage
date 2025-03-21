﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DtronixPackage.ViewModel;

public abstract class PackageManagerViewModel<TPackage, TPackageContent> : INotifyPropertyChanged
    where TPackage : Package<TPackageContent>, new()
    where TPackageContent : PackageContent, new()
{
    private readonly string _appName;
    public event PropertyChangedEventHandler? PropertyChanged;
    private TPackage? _package;
    private string _windowTitle = null!;
    private bool _addedModifiedText;

    private int _autoSavePeriod = 60 * 1000;

    /// <summary>
    /// Period between auto-saves in milliseconds. Defaults to 60 seconds.
    /// </summary>
    public int AutoSavePeriod
    {
        get => _autoSavePeriod;
        set
        {
            _autoSavePeriod = value;
            _ = _package?.ConfigureAutoSave(_autoSavePeriod, _autoSavePeriod, AutoSaveEnabled);
            OnPropertyChanged();
        }
    }

    private bool _autoSaveEnabled;
    /// <summary>
    ///  Set to true to enable auto-saving of packages.
    /// </summary>
    public bool AutoSaveEnabled
    {
        get => _autoSaveEnabled;
        set
        {
            _autoSaveEnabled = value;
            if (Package != null)
                Package.AutoSaveEnabled = value;
        }
    }

    /// <summary>
    /// The currently managed file by the Manager.
    /// </summary>
    public TPackage? Package {
        get => _package;
        protected set
        {
            if (_package != null)
                _package.MonitoredChanged -= PackageOnMonitoredChanged;

            _package = value;

            if (_package != null)
            {
                _package.MonitoredChanged += PackageOnMonitoredChanged;

                // Setup auto-saving.
                _ = _package.ConfigureAutoSave(_autoSavePeriod, _autoSavePeriod, AutoSaveEnabled);
            }

            SaveCommand.SetCanExecute(_package != null);
            SaveAsCommand.SetCanExecute(_package != null);
            CloseCommand.SetCanExecute(_package != null);

            OnPropertyChanged();

            PackageChanged?.Invoke(this, new PackageEventArgs<TPackage>(value));
        }
    }

    public event EventHandler<PackageEventArgs<TPackage>>? Created;
    public event EventHandler? Closed;
    public event EventHandler<PackageEventArgs<TPackage>>? Opened;
    public event EventHandler<PackageEventArgs<TPackage>>? PackageChanged;
        
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
        SaveCommand = new ActionCommand(SaveCommand_Execute, false);

        SaveAsCommand = new ActionCommand(SaveAsCommand_Execute, false);

        OpenCommand = new ActionCommand(OpenCommand_Execute, true);

        CloseCommand = new ActionCommand(CloseCommand_Execute, false);
        NewCommand = new ActionCommand(NewCommand_Execute, true);
    }

    protected abstract Task ShowMessage(PackageMessageEventArgs message);

    protected abstract void InvokeOnDispatcher(Action action);

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

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Implements the execution of <see cref="SaveCommand" />
    /// </summary>
    private async void SaveCommand_Execute()
    {
        try
        {
            await Save();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Implements the execution of <see cref="SaveAsCommand" />
    /// </summary>
    private async void SaveAsCommand_Execute()
    {
        try
        {
            await SaveAs();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Implements the execution of <see cref="NewCommand" />
    /// </summary>
    private async void NewCommand_Execute()
    {
        try
        {
            await New();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Implements the execution of <see cref="OpenCommand" />
    /// </summary>
    private async void OpenCommand_Execute()
    {
        try
        {
            await Open();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Implements the execution of <see cref="CloseCommand" />
    /// </summary>
    private async void CloseCommand_Execute()
    {
        try
        {
            await TryClose();
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Attempts to close the currently open package.
    /// If there are modifications, it prompts for confirmation about saving.
    /// </summary>
    /// <returns>
    /// True if close has succeeded closing or the there is no package open.
    /// False if the user has provided input to stop the closing process.
    /// </returns>
    protected internal virtual async Task<bool> TryClose()
    {
        if (Package == null) 
            return true;

        if (Package.IsContentModified)
        {
            if (await AskSave() == MessageBoxResult.Cancel)
                return false;
        }

        Package.Close();
        Package = null;
        Closed?.Invoke(this, EventArgs.Empty);
        StatusChange();

        return true;
    }

    public virtual async Task<bool> New()
    {

        // Attempt to close if any package is open.
        if (!await TryClose())
            return false;

        var file = new TPackage();

        Created?.Invoke(this, new PackageEventArgs<TPackage>(file));

        var saveResult = await SaveInternal(file);

        // If we have saved, set the Package property.
        if (saveResult)
            Package = file;
            
        StatusChange();

        return saveResult;
    }

    public virtual async Task<bool> Open()
    {
        if (!await TryClose())
            return false;

        var result = BrowseOpenFile(out var path, out var readOnly);

        if (result != true)
            return false;

        return await Open(path, readOnly);
    }

    public virtual async Task<bool> Open(string path, bool forceReadOnly)
    {
        // If there is already a file open, ask if you want to save the changes before opening another one.
        // Only applies if there are changes made to the file.
        if (!await TryClose())
            return false;

        var openFile = new TPackage();
        var result = await openFile.Open(path, forceReadOnly);
            
        // If the file is locked, give the option to open read-only.
        if (result.IsSuccessful == false)
        {
            if (result.Result == PackageOpenResultType.IncompatibleVersion
                || result.Result == PackageOpenResultType.IncompatiblePackageVersion)
            {
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.OK, 
                    "Can not open file.\r\n\r\n" + (result.Result == PackageOpenResultType.IncompatibleVersion 
                        ? $" Opened file version is {result.OpenVersion} while application is version {openFile.CurrentAppVersion}."
                        : $"Package version {result.OpenVersion} is incompatible."),
                    "Version Incompatible");

                await ShowMessage(message);
                    
                return false;
            }

            if (result.Result == PackageOpenResultType.UpgradeFailure)
            {
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.OK,
                    $"Can't open file. Failed upgrading file.\r\n\r\n{result.Exception?.Message}",
                    "Error Opening");

                await ShowMessage(message);

                return false;
            }
            else if (result.Result == PackageOpenResultType.FileNotFound)
            {
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.OK,
                    $"Scheduler file does not exist.\r\n\r\n{result.Exception?.Message}",
                    "File not Found");

                await ShowMessage(message);

                return false;
            }

            if (result.Result == PackageOpenResultType.UnknownFailure)
            {
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.OK,
                    $"Can't open file.\r\n\r\n{result.Exception}",
                    "Error Opening");

                await ShowMessage(message);
                    
                return false;
            }

                
            if (result.Result == PackageOpenResultType.Locked 
                || result.Result == PackageOpenResultType.PermissionFailure)
            {
                var readOnlyText = result.LockInfo != null 
                    ? $"{path} is currently opened by {result.LockInfo.Username} on {result.LockInfo.DateOpened:F}." 
                    : $"{path} Could not open the file for editing because it is in use or is read-only.";

                // Try opening read-only?
                var message = new PackageMessageEventArgs(
                    PackageMessageEventArgs.MessageType.YesNo,
                    readOnlyText + "\nWould you like to open the file read-only?",
                    "Open Confirmation");
                
                await ShowMessage(message);

                // If the user did not agree to opening read-only, there is nothing to do.
                if (message.Result != MessageBoxResult.Yes)
                    return false;

                result = await openFile.Open(path, true);
            }
        }

        if (result.IsSuccessful)
        {
            Package = openFile;
            StatusChange();
            Opened?.Invoke(this, new PackageEventArgs<TPackage>(Package));
            return true;
        }

        await ShowMessage(new PackageMessageEventArgs(
            PackageMessageEventArgs.MessageType.OK,
            $"{path} Could not open file because it is currently in use or is read-only.",
            "Can't open file"));

        StatusChange();
        return false;
    }

    public async Task<MessageBoxResult> AskSave()
    {
        var message = new PackageMessageEventArgs(
            PackageMessageEventArgs.MessageType.YesNoCancel,
            "Do you want to save the open file?",
            "Save");
        await ShowMessage(message);

        if (message.Result != MessageBoxResult.Yes) 
            return message.Result;

        // If the save was not successful, cancel.
        if(!await Save())
            return MessageBoxResult.Cancel;

        return message.Result;
    }

    internal virtual async Task<bool> SaveInternal(TPackage? package)
    {
        if (package == null)
            return false;

        // File is not saved and Read-only considerations.
        if (string.IsNullOrEmpty(package.SavePath)
            && !System.IO.File.Exists(package.SavePath) || package.IsReadOnly)
        {
            return await SaveAsInternal(package);
        }

        var result = await package.Save(package.SavePath);
        StatusChange();

        if (!result.IsSuccessful)
        {
            await ShowMessage(new PackageMessageEventArgs(
                PackageMessageEventArgs.MessageType.OK,
                "Could not save package.\r\n\r\nError:"+result.Exception,
                "Error Saving"));
        }
            
        return result == PackageSaveResult.Success;
    }

    public Task<bool> Save()
    {
        return SaveInternal(Package);
    }

    internal virtual async Task<bool> SaveAsInternal(TPackage? package)
    {
        if (package == null)
            return false;

        if (BrowseSaveFile(out var path))
        {
            var result = await package.Save(path);

            switch (result.SaveResult)
            {
                case PackageSaveResultType.Locked:
                    await ShowMessage(new PackageMessageEventArgs(
                        PackageMessageEventArgs.MessageType.OK,
                        "Selected file is locked by another application.  Please select another location.",
                        "Saving Error"));
                    return await SaveAsInternal(package);

                case PackageSaveResultType.Failure:
                    await ShowMessage(new PackageMessageEventArgs(
                        PackageMessageEventArgs.MessageType.OK,
                        "File could not be saved at specified location.  Please select another location.",
                        "Saving Error"));
                    return await SaveAsInternal(package);
            }

            StatusChange();
            return true;
        }

        StatusChange();
        return false;
    }

    public Task<bool> SaveAs()
    {
        return SaveAsInternal(Package);
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
            if(Package?.IsContentModified == true) 
                WindowTitle += " (Modified)";

            OnPropertyChanged(nameof(IsReadOnly));
        });
    }

    private void PackageOnMonitoredChanged(object? sender, EventArgs e)
    {
        InvokeOnDispatcher(() => {          
            // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
            SaveCommand.SetCanExecute(Package?.IsContentModified == true);
        });

        if (_addedModifiedText || Package?.IsContentModified == false)
            return;

        _addedModifiedText = true;
        InvokeOnDispatcher(() =>
        {
            WindowTitle += " (Modified)"; 

            // Change the save state to ensure we only save when there are changes.  Leave SaveAs alone.
            SaveCommand.SetCanExecute(Package?.IsContentModified == true);
        });
    }
}