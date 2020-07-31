using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DtronixPackage.ViewModel;
using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class PackageManagerViewModelTests_Methods : PackageManagerViewModelTestBase
    {
        [Test]
        public async Task New_ReturnsTrueOnSuccessfulPathSelection()
        {
            Assert.IsTrue(await ViewModel.New());
        }

        [Test]
        public async Task New_SetsPackageProperty()
        {
            await ViewModel.New();
            Assert.IsNotNull(ViewModel.Package);
        }

        [Test]
        public async Task New_ReturnsFalseOnPathSelectionCancellation()
        {
            ViewModel.BrowsingSave = args => args.Result = false;
            Assert.IsFalse(await ViewModel.Open());
        }

        [Test]
        public async Task Open_DoesNotSetPackagePropertyOnPathSelectionCancellation()
        {
            ViewModel.BrowsingOpen = args => args.Result = false;
            await ViewModel.Open();
            Assert.IsNull(ViewModel.Package);
        }

        [Test]
        public async Task Open_ReturnsTrueOnSuccessfulPathSelection()
        {
            await CreateApplicationPackage();
            Assert.IsTrue(await ViewModel.Open());
        }

        [Test]
        public async Task Open_SetsPackageProperty()
        {
            await CreateApplicationPackage();
            await ViewModel.Open();
            Assert.IsNotNull(ViewModel.Package);
        }

        [Test]
        public async Task Open_OpensPackageReadOnly()
        {
            await CreateApplicationPackage();
            ViewModel.BrowsingOpen = args =>
            {
                args.ReadOnly = true;
                args.Path = PackageFilename;
                args.Result = true;
            };
            await ViewModel.Open();

            Assert.IsTrue(ViewModel.Package.IsReadOnly);
        }

        [Test]
        public async Task Open_OpensPackageForEditing()
        {
            await CreateApplicationPackage();
            ViewModel.BrowsingOpen = args =>
            {
                args.ReadOnly = false;
                args.Path = PackageFilename;
                args.Result = true;
            };
            await ViewModel.Open();

            Assert.IsFalse(ViewModel.Package.IsReadOnly);
        }

        [Test]
        public async Task Open_OpensReadOnlyPackage()
        {
            await CreateApplicationPackage();

            // Lock the file.
            var fs = new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

            ViewModel.ShowMessage += (sender, args) => args.Result = MessageBoxResult.Yes;

            ViewModel.BrowsingOpen = args =>
            {
                args.ReadOnly = false;
                args.Path = PackageFilename;
                args.Result = true;
            };
            await ViewModel.Open();

            Assert.IsTrue(ViewModel.Package.IsReadOnly);
        }

        [Test]
        public async Task Open_FailsOpeningExclusivelyLockedPackage()
        {
            await CreateApplicationPackage();

            // Lock the file.
            var fs = new FileStream(PackageFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            ViewModel.ShowMessage += (sender, args) => args.Result = MessageBoxResult.Yes;

            ViewModel.BrowsingOpen = args =>
            {
                args.ReadOnly = false;
                args.Path = PackageFilename;
                args.Result = true;
            };
            Assert.IsFalse(await ViewModel.Open());
        }


        [Test]
        public async Task Open_ReturnsFalseOnPathSelectionCancellation()
        {
            ViewModel.BrowsingSave = args => args.Result = false;
            Assert.IsFalse(await ViewModel.Open());
        }

        [Test]
        public async Task New_DoesNotSetPackagePropertyOnPathSelectionCancellation()
        {
            ViewModel.BrowsingSave = args => args.Result = false;
            await ViewModel.New();
            Assert.IsNull(ViewModel.Package);
        }

        [Test]
        public async Task TryClose_ClosesOpenPackage()
        {
            await ViewModel.New();
            Assert.IsTrue(await ViewModel.TryClose());
        }

        [Test]
        public async Task TryClose_ReturnsTrueWhenAlreadyClosed()
        {
            Assert.IsTrue(await ViewModel.TryClose());
        }




    }
}
