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
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 2)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 3)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 4)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 8)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 7)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 6)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 5))
            };


            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 1)), Is.EqualTo(0));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 2)), Is.EqualTo(1));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 3)), Is.EqualTo(2));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 4)), Is.EqualTo(3));

            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 5)), Is.EqualTo(4));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 6)), Is.EqualTo(5));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 7)), Is.EqualTo(6));
            Assert.That(upgradeManager.OrderedUpgrades.IndexOfKey(new Version(0, 8)), Is.EqualTo(7));
        }

        [Test]
        public void UpgradeManagerAddsPackagesInOrder()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 3)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 1)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 4)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 2)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 3))
            };

            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 1)), Is.EqualTo(0));
            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 2)), Is.EqualTo(1));

            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 3)), Is.EqualTo(1));
            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 4)), Is.EqualTo(2));
        }

        [Test]
        public void UpgradeManagerOrdersApplicationBetweenPackageUpgrades()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 3)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 1)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 2))
            };

            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 1)), Is.EqualTo(0));
            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 2)].IndexOfKey(new Version(0, 2)), Is.EqualTo(1));
        }

        [Test]
        public void UpgradeManagerEnumeratesInOrder()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 4), new Version(0, 5)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 3), new Version(0, 3)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 2)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 3), new Version(0, 4)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 1))
            };

            VersionedAppUpgrade previous = null;
            foreach (var upgrade in upgradeManager)
            {
                if(upgrade is not VersionedAppUpgrade appUpgrade)
                    continue;

                if (previous != null)
                    Assert.That(appUpgrade.AppVersion, Is.GreaterThan(previous.AppVersion));
                
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
                    new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                    new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1))
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
                    new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 1)),
                    new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 1))
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
                    new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 1)),
                    new VersionedAppUpgrade(new PackageUpgradeVersion(0, 2), new Version(0, 1))
                };
            });
        }

        [Test]
        public void UpgradeManagerOrdersApplicationUpgrades()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version())
            {
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 1)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 2)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 4)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 3))
            };

            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 1)), Is.EqualTo(1));
            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 2)), Is.EqualTo(2));

            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 3)), Is.EqualTo(3));
            Assert.That(upgradeManager.OrderedUpgrades[new Version(0, 1)].IndexOfKey(new Version(0, 4)), Is.EqualTo(4));
        }

        [Test]
        public void UpgradeManagerAddsOnlyInternalUpgradesPastCurrentVersion()
        {
            var upgradeManager = new UpgradeManager(new Version(0, 1), new Version())
            {
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 1)),
                new VersionedInterUpgrade(new PackageUpgradeVersion(0, 3))
            };

            Assert.That(upgradeManager.Count(), Is.EqualTo(1));
        }

        [Test]
        public void UpgradeManagerAddsOnlyApplicationUpgradesPastCurrentVersion()
        {
            var upgradeManager = new UpgradeManager(new Version(), new Version(0, 2))
            {
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 2)),
                new VersionedAppUpgrade(new PackageUpgradeVersion(0, 1), new Version(0, 4))
            };

            Assert.That(upgradeManager.Count(), Is.EqualTo(1));
        }

        private class VersionedInterUpgrade : InternalPackageUpgrade
        {
            public VersionedInterUpgrade(PackageUpgradeVersion version)
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
            public VersionedAppUpgrade(PackageUpgradeVersion dependentPackageVersion, Version appVersion)
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