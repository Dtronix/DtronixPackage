using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage.Tests.IntegrationTests
{
    // ReSharper disable once InconsistentNaming
    class ApplicationPackageUpgradeCallback : ApplicationPackageUpgrade
    {

        private readonly Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> Upgrading;

        public ApplicationPackageUpgradeCallback(
            Version packageDependentVersion,
            Version appVersion, 
            Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> upgrading)
            : base(packageDependentVersion, appVersion)
        {
            Upgrading = upgrading;
        }

        public ApplicationPackageUpgradeCallback(
            Version packageDependentVersion,
            Version appVersion,
            Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task> upgrading)
            : base(packageDependentVersion, appVersion)
        {
            Upgrading = args =>
            {
                upgrading(args);
                return Task.FromResult(true);
            };
        }

        protected override async Task<bool> OnUpgrade(ZipArchive archive)
        {
            var args = new DtronixPackageCallbackUpgradeCallbackEventArgs()
            {
                Archive = archive
            };
            return await Upgrading(args);
        }
    }

    class DtronixPackageCallbackUpgradeCallbackEventArgs {
        public ZipArchive Archive { get; set; }
    }
}
