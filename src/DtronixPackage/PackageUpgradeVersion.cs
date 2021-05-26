using System;

namespace DtronixPackage
{
    public static class PackageUpgradeVersion
    {
        public static Version Unversioned { get; } = new Version();
        public static Version V1_1_0 { get; } = new Version(1, 1, 0);
    }
}