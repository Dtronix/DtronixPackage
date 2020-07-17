using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using DtronixPackage;

namespace DtronixPackage.Tests.IntegrationTests
{
    // ReSharper disable once InconsistentNaming
    class PackageCallbackUpgrade : PackageUpgrade<FileContent>
    {

        private readonly Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> Upgrading;

        public PackageCallbackUpgrade(
            Version version, 
            Func<DtronixPackageCallbackUpgradeCallbackEventArgs, Task<bool>> upgrading)
            : base(version)
        {
            Upgrading = upgrading;
        }

        public PackageCallbackUpgrade(
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
