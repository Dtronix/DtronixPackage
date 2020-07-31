using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.ViewModel;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class TestPackageManagerViewModel : PackageManagerViewModel<PackageManagerViewModelPackage, PackageContent>
    {
        public Action<BrowseEventArgs> BrowsingOpen;
        public Action<BrowseEventArgs> BrowsingSave;

        public Func<PackageManagerViewModelPackage, Task<bool>> PackageOpening;

        public Func<PackageManagerViewModelPackage, Task> PackageSaving;

        public TestPackageManagerViewModel()
            : base("DtronixPackage.Tests")
        {
            Created += OnCreated;
        }

        private void OnCreated(object? sender, PackageEventArgs<PackageManagerViewModelPackage> e)
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

        protected override bool BrowseSaveFile(out string path)
        {
            var args = new BrowseEventArgs();
            BrowsingSave?.Invoke(args);
            path = args.Path;
            return args.Result;
        }
    }
}
