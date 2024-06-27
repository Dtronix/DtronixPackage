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
            Assert.That(ViewModel.CloseCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void PackageNotOpen_CanNotSaveAs()
        {
            Assert.That(ViewModel.SaveAsCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void PackageNotOpen_CanNotSave()
        {
            Assert.That(ViewModel.SaveCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void PackageNotOpen_CanOpen()
        {
            Assert.That(ViewModel.OpenCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void PackageNotOpen_CanNew()
        {
            Assert.That(ViewModel.NewCommand.CanExecute(null), Is.True);
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
            Assert.That(await ViewModel.NewComplete.Task.Timeout(1000), Is.False);
        }

        [Test]
        public async Task PackageNotOpen_NewSetsPackageProperty()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.That(await ViewModel.NewComplete.Task.Timeout(1000), Is.True);
            Assert.That(ViewModel.Package, Is.Not.Null);
        }

        [Test]
        public async Task NewPackageOpen_Closes()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.That(await ViewModel.NewComplete.Task.Timeout(1000), Is.True);
            ViewModel.CloseCommand.Execute(null);

            await ViewModel.CloseComplete.Task.Timeout(1000);
            Assert.That(File.Exists(PackageFilename), Is.True);
            File.Delete(PackageFilename);
            Assert.That(ViewModel.Package, Is.Null);
        }

        [Test]
        public async Task PackageOpen_CanClose()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
            Assert.That(ViewModel.CloseCommand.CanExecute(null), Is.True);
        }     
        
        [Test]
        public async Task PackageOpen_CanSave()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
            Assert.That(ViewModel.SaveCommand.CanExecute(null), Is.True);
        }     
        
        [Test]
        public async Task PackageOpen_CanSaveAs()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewComplete.Task.Timeout(1000);
            Assert.That(ViewModel.SaveAsCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void PackageOpen_CanOpen()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.That(ViewModel.OpenCommand.CanExecute(null), Is.True);
        }

        [Test]
        public void PackageOpen_CanNew()
        {
            ViewModel.NewCommand.Execute(null);
            Assert.That(ViewModel.NewCommand.CanExecute(null), Is.True);
        }

        
        public async Task<bool> PackageOpenAsksToSaveWith(Func<Task> action)
        {
            var success = false;
            void OnViewModelOnShowMessage(object sender, PackageMessageEventArgs args)
            {
                success = args.Type == PackageMessageEventArgs.MessageType.YesNoCancel;
                ViewModel.ShowMessageEvent -= OnViewModelOnShowMessage;
            }

            ViewModel.ShowMessageEvent += OnViewModelOnShowMessage;
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

            Assert.That(result, Is.True);
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

            Assert.That(result, Is.True);
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

            Assert.That(result, Is.True);
        }

        [Test]
        public async Task PackageOpen_NewDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.NewCommand.Execute(null);
                await ViewModel.NewComplete.Task.Timeout(1000);
            });

            Assert.That(result, Is.False);
        }


        [Test]
        public async Task PackageOpen_OpenDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.OpenCommand.Execute(null);
                await ViewModel.OpenComplete.Task.Timeout(1000);
            });

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task PackageOpen_CloseDoesNotAskToSave()
        {
            var result = await PackageOpenAsksToSaveWith(async () =>
            {
                ViewModel.CloseCommand.Execute(null);
                await ViewModel.CloseComplete.Task.Timeout(1000);
            });

            Assert.That(result, Is.False);
        }


    }
}
