using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DtronixPackage.ViewModel;
using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class PackageManagerViewModelTests_Commands : PackageManagerViewModelTestBase
    {

        [Test]
        public void PackageNotOpen_CanNotClose()
        {
            Assert.IsFalse(ViewModel.CloseCommand.CanExecute(null));
        }

        [Test]
        public void PackageNotOpen_CanNotSaveAs()
        {
            Assert.IsFalse(ViewModel.SaveAsCommand.CanExecute(null));
        }

        [Test]
        public void PackageNotOpen_CanNotSave()
        {
            Assert.IsFalse(ViewModel.SaveCommand.CanExecute(null));
        }

        [Test]
        public void PackageNotOpen_CanOpen()
        {
            Assert.IsTrue(ViewModel.OpenCommand.CanExecute(null));
        }

        [Test]
        public void PackageNotOpen_CanNew()
        {
            Assert.IsTrue(ViewModel.NewCommand.CanExecute(null));
        }

        [Test]
        public void PackageNotOpen_NewRequestsSaveDestination()
        {

            ViewModel.BrowsingSave = args => TestCompleted.Set();

            ViewModel.NewCommand.Execute(null);

            WaitForCompletion();
        }

        [Test]
        public async Task PackageNotOpen_NewCreatesFile()
        {
            ViewModel.NewCommand.Execute(null);

            await Utilities.AssertFileExistWithin(PackageFilename);
        }   
        
        [Test]
        public async Task PackageNotOpen_NewLocksFile()
        {
            ViewModel.NewCommand.Execute(null);
            await Utilities.AssertFileExistWithin(PackageFilename);
            Assert.Throws<IOException>(() => File.Delete(PackageFilename));
        }

        [Test]
        public async Task PackageNotOpen_NewFailsOFailsOnSaveCancel()
        {
            ViewModel.BrowsingSave = args => args.Result = false;
            ViewModel.NewCommand.Execute(null);
            Assert.IsFalse(await ViewModel.NewComplete.Task.Timeout(1000));
        }

        [Test]
        public async Task PackageNotOpen_NewSetsPackageProperty()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(await ViewModel.NewComplete.Task.Timeout(1000));
            Assert.IsNotNull(ViewModel.Package);
        }

        [Test]
        public async Task NewPackageOpen_Closes()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.IsTrue(await ViewModel.NewComplete.Task.Timeout(1000));
            ViewModel.CloseCommand.Execute(null);

            await ViewModel.CloseComplete.Task.Timeout(1000);
            Assert.IsTrue(File.Exists(PackageFilename));
            File.Delete(PackageFilename);
            Assert.IsNull(ViewModel.Package);
        }

        [Test]
        public async Task PackageOpen_CanClose()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
            Assert.IsTrue(ViewModel.CloseCommand.CanExecute(null));
        }     
        
        [Test]
        public async Task PackageOpen_CanSave()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
            Assert.IsTrue(ViewModel.SaveCommand.CanExecute(null));
        }     
        
        [Test]
        public async Task PackageOpen_CanSaveAs()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
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

        
        public async Task<bool> PackageOpenAsksToSaveWith(Func<Task> action)
        {
            var success = false;
            void OnViewModelOnShowMessage(object sender, PackageMessageEventArgs args)
            {
                success = args.Type == PackageMessageEventArgs.MessageType.YesNoCancel;
                ViewModel.ShowMessage -= OnViewModelOnShowMessage;
            }

            ViewModel.ShowMessage += OnViewModelOnShowMessage;
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);

            await action();

            return success;
        }

        [Test]
        public async Task PackageOpen_NewAsksToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.Package.ContentModifiedOverride();
                ViewModel.NewCommand.Execute(null);

                await ViewModel.NewComplete.Task.Timeout(1000);
            });

            Assert.IsTrue(result);
        }

        [Test]
        public async Task PackageOpen_OpenAsksToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.Package.ContentModifiedOverride();
                ViewModel.OpenCommand.Execute(null);

                await ViewModel.OpenComplete.Task.Timeout(1000);
            });

            Assert.IsTrue(result);
        }

        [Test]
        public async Task PackageOpen_CloseAsksToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.Package.ContentModifiedOverride();
                ViewModel.CloseCommand.Execute(null);
                await ViewModel.CloseComplete.Task.Timeout(1000);
            });

            Assert.IsTrue(result);
        }

        [Test]
        public async Task PackageOpen_NewDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.NewCommand.Execute(null);
                await ViewModel.NewComplete.Task.Timeout(1000);
            });

            Assert.IsFalse(result);
        }


        [Test]
        public async Task PackageOpen_OpenDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.OpenCommand.Execute(null);
                await ViewModel.OpenComplete.Task.Timeout(1000);
            });

            Assert.IsFalse(result);
        }

        [Test]
        public async Task PackageOpen_CloseDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.CloseCommand.Execute(null);
                await ViewModel.CloseComplete.Task.Timeout(1000);
            });

            Assert.IsFalse(result);
        }


    }
}
