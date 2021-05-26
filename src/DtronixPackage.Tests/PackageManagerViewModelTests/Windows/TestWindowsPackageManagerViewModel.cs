using System;
using System.Threading.Tasks;
using DtronixPackage.ViewModel;

namespace DtronixPackage.Tests.PackageManagerViewModelTests.Windows
{
    public class TestWindowsPackageManagerViewModel : WindowsPackageManagerViewModel<PackageManagerViewModelPackage, TestPackageContent>
    {
        public Action<BrowseEventArgs> BrowsingOpen;
        public Action<BrowseEventArgs> BrowsingSave;

        public Func<PackageReader, PackageManagerViewModelPackage, Task<bool>> PackageOpening;

        public Func<PackageWriter, PackageManagerViewModelPackage, Task> PackageSaving;

        public TaskCompletionSource<bool> NewPackageCreated = new TaskCompletionSource<bool>();

        public void ResetCompleteTasks()
        {
            NewPackageCreated = new TaskCompletionSource<bool>();
        }

        public TestWindowsPackageManagerViewModel()
            : base("DtronixPackage.Tests")
        {
            Created += OnCreated;
            ResetCompleteTasks();
        }

        private void OnCreated(object sender, PackageEventArgs<PackageManagerViewModelPackage> e)
        {
            e.Package.Saving = PackageSaving;
            e.Package.Opening = PackageOpening;
        }

        protected override bool BrowseOpenFile(out string path, out bool openReadOnly)
        {
            var args = new BrowseEventArgs();
            BrowsingOpen?.Invoke(args);
            path = args.Path;
            openReadOnly = args.ReadOnly;
            return args.Result;
        }

        public override string FileFilter { get; } = "Temp|*.tmp";
        public override string DefaultPackageName { get; } = "Default.temp";

        protected override bool BrowseSaveFile(out string path)
        {
            var args = new BrowseEventArgs();
            BrowsingSave?.Invoke(args);
            path = args.Path;
            return args.Result;
        }

        public override async Task<bool> New()
        {
            var result = await base.New();
            NewPackageCreated.SetResult(true);
            return result;
        }
    }
}
