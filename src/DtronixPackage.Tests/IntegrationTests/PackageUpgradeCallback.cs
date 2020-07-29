using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage.Tests.IntegrationTests
{
    // ReSharper disable once InconsistentNaming
    class PackageUpgradeCallback : PackageUpgrade
    {

        private readonly Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> Upgrading;

        public PackageUpgradeCallback(
            Version version, 
            Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> upgrading)
            : base(version)
        {
            Upgrading = upgrading;
        }

        public PackageUpgradeCallback(
            Version version, 
            Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task> upgrading)
            : base(version)
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
