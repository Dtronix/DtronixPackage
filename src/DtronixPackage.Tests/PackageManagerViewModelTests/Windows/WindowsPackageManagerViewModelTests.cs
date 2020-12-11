using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DtronixPackage.ViewModel;
using NUnit.Framework;

namespace DtronixPackage.Tests.PackageManagerViewModelTests.Windows
{
    public class WindowsPackageManagerViewModelTests : WindowsPackageManagerViewModelTestBase
    {
        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task WindowClosing_ClosesAfterCompletion()
        {
            var fullySaved = false;
            var windowClosed = new TaskCompletionSource<bool>();

            ViewModel.NewCommand.Execute(null);

            await ViewModel.NewPackageCreated.Task;

            ViewModel.ShowMessage += (sender, args) =>
            {
                args.Result = MessageBoxResult.Yes;
                Console.WriteLine("Message: " + args.MessageBoxText);
            };

            await ViewModel.Save();

            ViewModel.Package.Saving = async package =>
            {
                Console.WriteLine("Saving started.");
                await Task.Delay(200);
                fullySaved = true;
                Console.WriteLine("Saving completed.");
            };


            ViewModel.Package.ContentModifiedOverride();

            var cancelEventArgs = new CancelEventArgs();

            var window = new Window();

            Console.WriteLine("OnWidowClosing called.");
            ViewModel.OnWidowClosing(window, cancelEventArgs);

            window.Closed += (sender, args) =>
            {
                Console.WriteLine("Window Closed");
                windowClosed.SetResult(true);
            };

            Assert.IsTrue(await windowClosed.Task);
            Assert.IsTrue(fullySaved);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task WindowClosing_CancelStopsWindowClose()
        {
            var fullySaved = false;
            var windowClosed = new TaskCompletionSource<bool>();

            ViewModel.NewCommand.Execute(null);

            await ViewModel.NewPackageCreated.Task;

            ViewModel.Package.Saving = async package =>
            {
                Console.WriteLine("Saving started.");
                await Task.Delay(200);
                fullySaved = true;
                Console.WriteLine("Saving completed.");
            };

            ViewModel.ShowMessage += (sender, args) =>
            {
                args.Result = MessageBoxResult.Cancel;
                Console.WriteLine("Message: " + args.MessageBoxText);
            };

            await ViewModel.Save();

            ViewModel.Package.ContentModifiedOverride();

            var cancelEventArgs = new CancelEventArgs();

            var window = new Window();

            Console.WriteLine("OnWidowClosing called.");
            ViewModel.OnWidowClosing(window, cancelEventArgs);

            window.Closed += (sender, args) =>
            {
                Console.WriteLine("Window Closed");
                windowClosed.SetResult(true);
            };

            //Assert.IsTrue(await windowClosed.Task);
            Assert.IsTrue(fullySaved);
        }

        [Test]
        [Apartment(ApartmentState.STA)]
        public async Task WindowClosing_CancelsClosingAction()
        {
            ViewModel.NewCommand.Execute(null);
            await ViewModel.NewPackageCreated.Task;
            var cancelEventArgs = new CancelEventArgs();
            var window = new Window();
            ViewModel.OnWidowClosing(window, cancelEventArgs);
            Assert.IsTrue(cancelEventArgs.Cancel);
        }

    }


}
