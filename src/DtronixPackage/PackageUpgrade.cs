using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public abstract class PackageUpgrade
    {
        public PackageUpgradeVersion DependentPackageVersion { get; }

        internal PackageUpgrade(PackageUpgradeVersion dependentPackageVersion)
        {
            DependentPackageVersion = dependentPackageVersion;
        }

        public Task<bool> Upgrade(ZipArchive archive)
        {
            return OnUpgrade(archive);
        }

        protected abstract Task<bool> OnUpgrade(ZipArchive archive);
    }

    public abstract class ApplicationPackageUpgrade : PackageUpgrade
    {
        public Version AppVersion { get; }

        protected ApplicationPackageUpgrade(PackageUpgradeVersion dependentPackageVersion, Version appVersion)
            : base(dependentPackageVersion)
        {
            AppVersion = appVersion;
        }
    }

    internal abstract class InternalPackageUpgrade : PackageUpgrade
    {
        protected InternalPackageUpgrade(PackageUpgradeVersion version)
            : base(version)
        {
        }
    }
}
