using System;
using Microsoft.Win32;

namespace DtronixPackage.ViewModel
{
    public abstract class WindowsPackageManagerViewModel<TPackage, TPackageContent> : PackageManagerViewModel<TPackage, TPackageContent>
        where TPackage : Package<TPackageContent>, new()
        where TPackageContent : PackageContent, new()
    {
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
    }
}
