using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public abstract class PackageUpgrade
    {
        public Version DependentPackageVersion { get; }

        protected PackageUpgrade(Version dependentPackageVersion)
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

        protected ApplicationPackageUpgrade(Version dependentPackageVersion, Version appVersion)
            : base(dependentPackageVersion)
        {
            AppVersion = appVersion;
        }
    }

    internal abstract class InternalPackageUpgrade : PackageUpgrade
    {
        protected InternalPackageUpgrade(Version version)
            : base(version)
        {
        }
    }
}
