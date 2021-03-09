using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DtronixPackage
{
    public class UpgradeManager
    {
        internal readonly List<PackageUpgrade> OrderedUpgrades = new();

        public void Add(PackageUpgrade upgrade)
        {
            if (OrderedUpgrades.Count == 0)
            {
                OrderedUpgrades.Add(upgrade);
                return;
            }

            var count = OrderedUpgrades.Count;
            PackageUpgrade previous;
            for (int i = 0; i < count; i++)
            {
                var current = OrderedUpgrades[i];
                if (upgrade.DependentPackageVersion >= current.DependentPackageVersion)
                {
                    // Find app version location.
                }

                previous = current;
            }

        }
    }
}
