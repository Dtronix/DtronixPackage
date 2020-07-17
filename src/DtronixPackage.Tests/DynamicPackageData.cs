using System;
using System.ComponentModel;

namespace DtronixPackage.Tests
{
    class DynamicPackageData : DynamicPackage
    {
        public PackageDataContractRoot Data { get; private set; }

        public DynamicPackageData(Version appVersion, IntegrationTestBase integrationTest)
            : base(appVersion, integrationTest, false, false)
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
