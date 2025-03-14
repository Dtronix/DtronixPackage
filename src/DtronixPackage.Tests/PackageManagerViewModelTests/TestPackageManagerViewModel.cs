using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage.ViewModel;

namespace DtronixPackage.Tests.PackageManagerViewModelTests
{
    public class TestPackageManagerViewModel : PackageManagerViewModel<PackageManagerViewModelPackage, TestPackageContent>
    {
        public Action<BrowseEventArgs> BrowsingOpen;
        public Action<BrowseEventArgs> BrowsingSave;

        public Func<PackageReader, PackageManagerViewModelPackage, Task<bool>> PackageOpening;

        public Func<PackageWriter, PackageManagerViewModelPackage, Task> PackageSaving;

        public TaskCompletionSource<bool> SaveAsComplete;
        public TaskCompletionSource<bool> SaveComplete;
        public TaskCompletionSource<bool> OpenComplete;
        public TaskCompletionSource<bool> NewComplete;
        public TaskCompletionSource<bool> CloseComplete;

        public event Action<TestPackageManagerViewModel, PackageMessageEventArgs> ShowMessageEvent;

        public void ResetCompleteTasks()
        {
            SaveAsComplete = new TaskCompletionSource<bool>();
            SaveComplete = new TaskCompletionSource<bool>();
            OpenComplete = new TaskCompletionSource<bool>();
            NewComplete = new TaskCompletionSource<bool>();
            CloseComplete = new TaskCompletionSource<bool>();
        }

        public TestPackageManagerViewModel()
            : base("DtronixPackage.Tests")
        {
            Created += OnCreated;
            ResetCompleteTasks();
        }

        protected override Task ShowMessage(PackageMessageEventArgs message)
        {
            ShowMessageEvent?.Invoke(this, message);
            return Task.CompletedTask;
        }

        protected override void InvokeOnDispatcher(Action action)
        {
            action?.Invoke();
        }

        private void OnCreated(object sender, PackageEventArgs<PackageManagerViewModelPackage> e)
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

        internal override async Task<bool> SaveAsInternal(PackageManagerViewModelPackage package)
        {
            var result = await base.SaveAsInternal(package);
            SaveAsComplete.SetResult(result);
            return result;
        }

        internal override async Task<bool> SaveInternal(PackageManagerViewModelPackage package)
        {
            var result = await base.SaveInternal(package);
            SaveComplete.SetResult(result);
            return result;
        }

        public override async Task<bool> Open(string path, bool forceReadOnly)
        {
            var result = await base.Open(path, forceReadOnly);
            OpenComplete.SetResult(result);
            return result;
        }

        public override async Task<bool> New()
        {
            var result = await base.New();
            NewComplete.SetResult(result);
            return result;
        }

        protected internal override async Task<bool> TryClose()
        {
            var result = await base.TryClose();
            CloseComplete.TrySetResult(true);
            return result;
        }
    }
}
