using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public abstract class PackageManagerViewModelTestBase
    {

        public TestPackageManagerViewModel ViewModel { get; set; }

        [SetUp]
        public virtual void Setup()
        {
            ViewModel = new TestPackageManagerViewModel();
        }
    }
}