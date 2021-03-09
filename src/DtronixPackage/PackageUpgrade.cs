using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public abstract class PackageUpgrade
    {
        public Version DependentPackageVersion { get; }
        public Version AppVersion { get; }

        protected PackageUpgrade(Version dependentPackageVersion, Version appVersion)
        {
            DependentPackageVersion = dependentPackageVersion;
            AppVersion = appVersion;
        }

        public Task<bool> Upgrade(ZipArchive archive)
        {
            return OnUpgrade(archive);
        }

        protected abstract Task<bool> OnUpgrade(ZipArchive archive);
    }
}
