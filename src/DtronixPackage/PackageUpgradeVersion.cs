using System;

namespace DtronixPackage;

public class PackageUpgradeVersion
{
    public static PackageUpgradeVersion Unversioned { get; } = new();
    public static PackageUpgradeVersion V1_1_0 { get; } = new(1, 1, 0);

    public Version Version { get; }

    internal PackageUpgradeVersion()
    {
        Version = new Version();
    }

    internal PackageUpgradeVersion(int major, int minor)
    {
        Version = new Version(major, minor);
    }

    internal PackageUpgradeVersion(int major, int minor, int build)
    {
        Version = new Version(major, minor, build);
    }

    internal PackageUpgradeVersion(int major, int minor, int build, int revision)
    {
        Version = new Version(major, minor, build, revision);
    }
}