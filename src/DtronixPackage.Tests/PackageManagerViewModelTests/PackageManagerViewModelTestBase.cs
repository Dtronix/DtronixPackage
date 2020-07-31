using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public abstract class PackageManagerViewModelTestBase
    {

        public TestPackageManagerViewModel ViewModel { get; set; }

        public ManualResetEventSlim TestCompleted { get; set; }

        public string PackageFilename { get; set; }

        [SetUp]
        public virtual void Setup()
        {
            ViewModel = new TestPackageManagerViewModel();
            TestCompleted = new ManualResetEventSlim();
            PackageFilename = Path.Combine("saves/", Guid.NewGuid() + ".file");

            ViewModel.BrowsingSave = args =>
            {
                args.Path = PackageFilename;
                args.Result = true;
            };

            ViewModel.BrowsingOpen = args =>
            {
                args.Path = PackageFilename;
                args.Result = true;
                args.ReadOnly = false;
            };

        }



        protected void WaitForCompletion(int timeout = 2000)
        {
            TestCompleted.Wait(timeout);
        }
    }
}