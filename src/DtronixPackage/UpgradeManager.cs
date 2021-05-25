using System;
using System.Collections;
using System.Collections.Generic;

namespace DtronixPackage
{
    /// <summary>
    /// Handles ordering and truncating of upgrades to be applied to packages.
    /// </summary>
    internal class UpgradeManager : IEnumerable<PackageUpgrade>
    {
        private readonly Version _currentInternalPackageVersion;
        private readonly Version _currentAppPackageVersion;

        internal readonly SortedList<Version, SortedList<Version, PackageUpgrade>> OrderedUpgrades = new();

        private readonly List<Version> _applicationUpgradeVersions = new();

        public UpgradeManager(Version currentInternalPackageVersion, Version currentAppPackageVersion)
        {
            _currentInternalPackageVersion = currentInternalPackageVersion;
            _currentAppPackageVersion = currentAppPackageVersion;
        }

        /// <summary>
        /// Adds the upgrade into the list of upgrades to apply.
        /// </summary>
        /// <param name="upgrade">Upgrade to add.</param>
        public void Add(PackageUpgrade upgrade)
        {
            void AddAppUpgrade(SortedList<Version, PackageUpgrade> upgradeList, ApplicationPackageUpgrade upg)
            {
                if (upg.AppVersion <= _currentAppPackageVersion)
                    return;

                if (_applicationUpgradeVersions.Contains(upg.AppVersion))
                    throw new ArgumentException(
                        $"Duplicate application version added for version {upg.AppVersion}");

                _applicationUpgradeVersions.Add(upg.AppVersion);

                upgradeList.Add(upg.AppVersion, upgrade);
            }

            void AddPackageUpgrade(SortedList<Version, PackageUpgrade> upgradeList, InternalPackageUpgrade upg)
            {
                if (upg.DependentPackageVersion <= _currentInternalPackageVersion)
                    return;

                upgradeList.Add(new Version(), upg);
            }

            if (OrderedUpgrades.ContainsKey(upgrade.DependentPackageVersion))
            {
                switch (upgrade)
                {
                    case ApplicationPackageUpgrade appUpgrade:
                        AddAppUpgrade(OrderedUpgrades[appUpgrade.DependentPackageVersion], appUpgrade);
                        break;

                    case InternalPackageUpgrade intUpgrade:
                        AddPackageUpgrade(OrderedUpgrades[intUpgrade.DependentPackageVersion], intUpgrade);
                        break;

                    default:
                        throw new ArgumentException("Unknown upgrade package passed.", nameof(upgrade));
                }
            }
            else
            {
                var upgradeList = new SortedList<Version, PackageUpgrade>();
                switch (upgrade)
                {
                    case ApplicationPackageUpgrade appUpgrade:
                        AddAppUpgrade(upgradeList, appUpgrade);
                        OrderedUpgrades.Add(appUpgrade.DependentPackageVersion, upgradeList);
                        break;

                    case InternalPackageUpgrade intUpgrade:
                        AddPackageUpgrade(upgradeList, intUpgrade);
                        OrderedUpgrades.Add(intUpgrade.DependentPackageVersion, upgradeList);
                        break;

                    default:
                        throw new ArgumentException("Unknown upgrade package passed.", nameof(upgrade));
                }
            }
        }

        public IEnumerator<PackageUpgrade> GetEnumerator()
        {
            foreach (var packageVersions in OrderedUpgrades)
            {
                foreach (var upgrades in packageVersions.Value)
                {
                    yield return upgrades.Value;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
