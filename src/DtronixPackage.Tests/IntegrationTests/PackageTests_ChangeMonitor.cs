using System;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class PackageTests_ChangeMonitor : IntegrationTestBase
    {

        private void MonitorChangesTests(DynamicPackageData file, Action test)
        {
            Assert.IsFalse(file.IsDataModified);
            file.Data.Children.Add(new PackageDataContractChild());
            test.Invoke();

            // Child property
            file.IsDataModified = false;
            file.Data.Children[0].Integer = 50;
            test.Invoke();

            // Child property
            file.IsDataModified = false;
            file.Data.Children[0].String = "new string";
            test.Invoke();

            // New child of child
            file.IsDataModified = false;
            file.Data.Children[0].Children.Add(new PackageDataContractChild());
            test.Invoke();

            // Child of child property
            file.IsDataModified = false;
            file.Data.Children[0].Children[0].Integer = 51;
            test.Invoke();

            // Moving child of child
            file.Data.Children[0].Children.Add(new PackageDataContractChild());
            file.IsDataModified = false;
            file.Data.Children[0].Children.Move(1, 0);
            test.Invoke();

            // Remove child of child
            file.IsDataModified = false;
            file.Data.Children[0].Children.RemoveAt(1);
            test.Invoke();
        }

        [Test]
        public void RegistersChanges()
        {
            var file = new DynamicPackageData(new Version(1,0), this);
            MonitorChangesTests(file, () => Assert.IsTrue(file.IsDataModified));
        }

        [Test]
        public void DeRegistersChanges()
        {
            var file = new DynamicPackageData(new Version(1,0), this);
            file.MonitorDeregisterOverride(file.Data);
            MonitorChangesTests(file, () => Assert.IsFalse(file.IsDataModified));
        }

        [Test]
        public void IgnoresChangesChanges()
        {
            var file = new DynamicPackageData(new Version(1,0), this);

            file.MonitorIgnore(() =>
            {
                file.Data.Children.Add(new PackageDataContractChild());
            });

            Assert.IsFalse(file.IsDataModified);
        }


    }
}