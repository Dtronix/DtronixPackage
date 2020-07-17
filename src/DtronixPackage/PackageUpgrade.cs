using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public abstract class PackageUpgrade<T>
        where T : PackageContent, new()
    {
        protected Package<T> File { get; private set; }
        public Version Version { get; }

        protected PackageUpgrade(Version version)
        {
            Version = version;
        }

        public Task<bool> Upgrade(Package<T> file, ZipArchive archive)
        {
            File = file;
            return OnUpgrade(archive);
        }

        protected abstract Task<bool> OnUpgrade(ZipArchive archive);
    }
}
