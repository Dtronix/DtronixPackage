using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using DtronixPackage.Upgrades;
using NUnit.Framework;

namespace DtronixPackage.Tests.UpgradeTests
{
    public class UpgradeManagerTests
    {
        [SetUp]
        public virtual Task Setup()
        {
            return Task.CompletedTask;

        }

        [Test]
        public void UpgradeManagerOrdersInterUpgrades()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedInterUpgrade(new Version(0, 1)),
                new VersionedInterUpgrade(new Version(0, 2)),
                new VersionedInterUpgrade(new Version(0, 3)),
                new VersionedInterUpgrade(new Version(0, 4)),
                new VersionedInterUpgrade(new Version(0, 8)),
                new VersionedInterUpgrade(new Version(0, 7)),
                new VersionedInterUpgrade(new Version(0, 6)),
                new VersionedInterUpgrade(new Version(0, 5))
            };


            Assert.AreEqual(0, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 1)));
            Assert.AreEqual(1, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 2)));
            Assert.AreEqual(2, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 3)));
            Assert.AreEqual(3, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 4)));

            Assert.AreEqual(4, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 5)));
            Assert.AreEqual(5, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 6)));
            Assert.AreEqual(6, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 7)));
            Assert.AreEqual(7, upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 8)));
        }

        [Test]
        public void UpgradeManagerAddsPackagesInOrder()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 3)),
                new VersionedAppUpgrade(new Version(0, 2), new Version(0, 1)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 4)),
                new VersionedAppUpgrade(new Version(0, 2), new Version(0, 2)),
                new VersionedInterUpgrade(new Version(0, 1)),
                new VersionedInterUpgrade(new Version(0, 3))
            };

            Assert.AreEqual(0, upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 1)));
            Assert.AreEqual(1, upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 2)));

            Assert.AreEqual(1, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 3)));
            Assert.AreEqual(2, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 4)));
        }

        [Test]
        public void UpgradeManagerOrdersApplicationBetweenPackageUpgrades()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedInterUpgrade(new Version(0, 1)),
                new VersionedInterUpgrade(new Version(0, 3)),
                new VersionedAppUpgrade(new Version(0, 2), new Version(0, 1)),
                new VersionedAppUpgrade(new Version(0, 2), new Version(0, 2))
            };

            Assert.AreEqual(0, upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 1)));
            Assert.AreEqual(1, upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 2)));
        }

        [Test]
        public void UpgradeManagerEnumeratesInOrder()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedAppUpgrade(new Version(0, 4), new Version(0, 5)),
                new VersionedAppUpgrade(new Version(0, 3), new Version(0, 3)),
                new VersionedAppUpgrade(new Version(0, 2), new Version(0, 2)),
                new VersionedAppUpgrade(new Version(0, 3), new Version(0, 4)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 1))
            };

            VersionedAppUpgrade previous = null;
            foreach (var upgrade in upgradeManager)
            {
                if(upgrade is not VersionedAppUpgrade appUpgrade)
                    continue;

                if (previous != null)
                    Assert.Greater(appUpgrade.AppVersion, previous.AppVersion);
                
                previous = appUpgrade;
            }
        }

        [Test]
        public void UpgradeManagerThrowsOnDuplicateInternalUpgrades()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new UpgradeManager(new Version(), new Version())
                {
                    new VersionedInterUpgrade(new Version(0, 1)),
                    new VersionedInterUpgrade(new Version(0, 1))
                };
            });
        }

        [Test]
        public void UpgradeManagerThrowsOnDuplicateApplicationUpgrades()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new UpgradeManager(new Version(), new Version())
                {
                    new VersionedAppUpgrade(new Version(0, 2), new Version(0, 1)),
                    new VersionedAppUpgrade(new Version(0, 2), new Version(0, 1))
                };
            });
        }

        [Test]
        public void UpgradeManagerThrowsOnDuplicateApplicationUpgradesAcrossInternalUpgrades()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                new UpgradeManager(new Version(), new Version())
                {
                    new VersionedAppUpgrade(new Version(0, 1), new Version(0, 1)),
                    new VersionedAppUpgrade(new Version(0, 2), new Version(0, 1))
                };
            });
        }

        [Test]
        public void UpgradeManagerOrdersApplicationUpgrades()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedInterUpgrade(new Version(0, 1)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 1)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 2)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 4)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 3))
            };

            Assert.AreEqual(1, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 1)));
            Assert.AreEqual(2, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 2)));

            Assert.AreEqual(3, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 3)));
            Assert.AreEqual(4, upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 4)));
        }

        [Test]
        public void UpgradeManagerAddsOnlyInternalUpgradesPastCurrentVersion()
        {
            var upgradeManager = new UpgradeManager(new Version(0, 1), new Version())
            {
                new VersionedInterUpgrade(new Version(0, 1)),
                new VersionedInterUpgrade(new Version(0, 3))
            };

            Assert.AreEqual(1, upgradeManager.Count());
        }

        [Test]
        public void UpgradeManagerAddsOnlyApplicationUpgradesPastCurrentVersion()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version(0, 2))
            {
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 2)),
                new VersionedAppUpgrade(new Version(0, 1), new Version(0, 4))
            };

            Assert.AreEqual(1, upgradeManager.Count());
        }

        private class VersionedInterUpgrade : InternalPackageUpgrade
        {
            public VersionedInterUpgrade(Version version)
                : base(version)
            {
            }

            protected override Task<bool> OnUpgrade(ZipArchive archive)
            {
                return Task.FromResult(true);
            }

            public override string ToString()
            {
                return $"Pkg {DependentPackageVersion}";
            }
        }

        private class VersionedAppUpgrade : ApplicationPackageUpgrade
        {
            public VersionedAppUpgrade(Version dependentPackageVersion, Version appVersion)
                : base(dependentPackageVersion, appVersion)
            {
            }

            protected override Task<bool> OnUpgrade(ZipArchive archive)
            {
                return Task.FromResult(true);
            }

            public override string ToString()
            {
                return $"Pkg {DependentPackageVersion} App {AppVersion}";
            }
        }
    }
}