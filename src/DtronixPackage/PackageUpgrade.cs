using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public abstract class PackageUpgrade
    {
        public Version Version { get; }

        protected PackageUpgrade(Version version)
        {
            Version = version;
        }

        public Task<bool> Upgrade(ZipArchive archive)
        {
            return OnUpgrade(archive);
        }

        protected abstract Task<bool> OnUpgrade(ZipArchive archive);
    }
}
