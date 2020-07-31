using System;
using System.Collections.Generic;
using System.Text;
using DtronixPackage.ViewModel;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class TestPackageManagerViewModel : PackageManagerViewModel<PackageManagerViewModelPackage, PackageContent>
    {
        public event EventHandler<BrowseEventArgs> BrowsingOpen;
        public event EventHandler<BrowseEventArgs> BrowsingSave;

        public TestPackageManagerViewModel()
            : base("DtronixPackage.Tests", new Version(1, 0, 0, 0))
        {
        }

        protected override bool BrowseOpenFile(out string path, out bool openReadOnly)
        {
            var args = new BrowseEventArgs();
            BrowsingOpen?.Invoke(this, args);
            path = args.Path;
            openReadOnly = args.ReadOnly;
            return args.Result;
        }

        protected override bool BrowseSaveFile(out string path)
        {
            var args = new BrowseEventArgs();
            BrowsingSave?.Invoke(this, args);
            path = args.Path;
            return args.Result;
        }
    }
}
