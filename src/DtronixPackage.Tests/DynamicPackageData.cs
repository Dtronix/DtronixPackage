using System;
using System.ComponentModel;
using DtronixPackage.Tests.IntegrationTests;

namespace DtronixPackage.Tests
{
    class DynamicPackageData : DynamicPackage
    {
        public PackageDataContractRoot Data { get; private set; }

        public DynamicPackageData(Version currentAppVersion, IntegrationTestBase integrationTest)
            : base(currentAppVersion, integrationTest, false, false)
        {
            Data = new PackageDataContractRoot();

            MonitorRegister(Data);
        }

        public void MonitorRegisterOverride<T>(T obj)
            where T : INotifyPropertyChanged
        {
            MonitorRegister(obj);
        }

        public void MonitorDeregisterOverride<T>(T obj)
            where T : INotifyPropertyChanged
        {
            MonitorDeregister(obj);
        }
    }
}
