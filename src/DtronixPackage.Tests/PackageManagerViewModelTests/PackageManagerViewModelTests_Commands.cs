using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DtronixPackage.ViewModel;
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

        [Test]
        public void PackageIsNotOpen_NewRequestsSaveDestination()
        {

            ViewModel.BrowsingSave = args => TestCompleted.Set();

            ViewModel.NewCommand.Execute(null);

            WaitForCompletion();
        }

        [Test]
        public async Task PackageIsNotOpen_NewCreatesFile()
        {
            ViewModel.NewCommand.Execute(null);

            await Utilities.AssertFileExistWithin(PackageFilename);
        }   
        
        [Test]
        public async Task PackageIsNotOpen_NewLocksFile()
        {
            ViewModel.NewCommand.Execute(null);
            await Utilities.AssertFileExistWithin(PackageFilename);
            Assert.Throws<IOException>(() => File.Delete(PackageFilename));
        }

        [Test]
        public async Task NewPackageOpen_Closes()
        {
            ViewModel.NewCommand.Execute(null);
            await Utilities.AssertFileExistWithin(PackageFilename);
            ViewModel.CloseCommand.Execute(null);
            File.Delete(PackageFilename);
        }

        [Test]
        public void PackageOpen_CanClose()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(ViewModel.CloseCommand.CanExecute(null));
        }     
        
        [Test]
        public void PackageOpen_CanSave()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(ViewModel.SaveCommand.CanExecute(null));
        }     
        
        [Test]
        public void PackageOpen_CanSaveAs()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(ViewModel.SaveAsCommand.CanExecute(null));
        }

        [Test]
        public void PackageOpen_CanOpen()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(ViewModel.OpenCommand.CanExecute(null));
        }

        [Test]
        public void PackageOpen_CanNew()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(ViewModel.NewCommand.CanExecute(null));
        }

        [Test]
        public void PackageOpen_OpenAsksToSave()
        {
            var success = false;

            void OnViewModelOnShowMessage(object? sender, PackageMessageEventArgs args)
            {
                success = args.Type == PackageMessageEventArgs.MessageType.YesNoCancel;
                ViewModel.ShowMessage -= OnViewModelOnShowMessage;
                TestCompleted.Set();
            }

            ViewModel.ShowMessage += OnViewModelOnShowMessage;
            ViewModel.NewCommand.Execute(null);
            ViewModel.OpenCommand.Execute(null);
            WaitForCompletion();

            Assert.IsTrue(success);
        }

    }
}
