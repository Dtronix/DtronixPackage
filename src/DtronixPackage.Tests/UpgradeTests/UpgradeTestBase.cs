using System.IO.Compression;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DtronixPackage.Tests.UpgradeTests
{
    public abstract class UpgradeTestBase<TUpgrade>
        where TUpgrade : PackageUpgrade, new()
    {
        protected PackageBuilder PackageBuilder;
        protected ZipArchive Package;
        protected TUpgrade UpgradeClass;

        [SetUp]
        public virtual async Task Setup()
        {
            PackageBuilder = new PackageBuilder("saves");
            await CreateAndUpgradePackage();
        }

        protected virtual async Task CreateAndUpgradePackage()
        {
            Package = await CreatePackage();
            UpgradeClass = new TUpgrade();
            await UpgradeClass.Upgrade(Package);
        }

        protected abstract Task<ZipArchive> CreatePackage();
    }
}