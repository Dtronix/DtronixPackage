using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class PackageManagerViewModelTests_Commands : PackageManagerViewModelTestBase
    {

        [Test]
        public void PackageIsNotOpen_CanNotClose()
        {
            Assert.IsFalse(ViewModel.CloseCommand.CanExecute(null));
        }

        [Test]
        public void PackageIsNotOpen_CanNotSaveAs()
        {
            Assert.IsFalse(ViewModel.SaveAsCommand.CanExecute(null));
        }

        [Test]
        public void PackageIsNotOpen_CanNotSave()
        {
            Assert.IsFalse(ViewModel.SaveCommand.CanExecute(null));
        }

        [Test]
        public void PackageIsNotOpen_CanOpen()
        {
            Assert.IsTrue(ViewModel.OpenCommand.CanExecute(null));
        }

        [Test]
        public void PackageIsNotOpen_CanNew()
        {
            Assert.IsTrue(ViewModel.NewCommand.CanExecute(null));
        }

    }
}
