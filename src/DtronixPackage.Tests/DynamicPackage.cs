using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DtronixPackage.Tests.IntegrationTests;

namespace DtronixPackage.Tests
{
    public class DynamicPackage : Package<PackageContent>
    {
        private readonly IntegrationTestBase _integrationTest;

        public Func<DynamicPackage, Task<bool>> Opening;

        public Func<DynamicPackage, Task> Saving;

        public Func<string> TempPackagePathRequest;

        public List<PackageUpgrade> UpgradeOverrides => Upgrades;

        public DateTimeOffset? DateTimeOffsetOverride { get; set; }

        internal override DateTimeOffset CurrentDateTimeOffset => DateTimeOffsetOverride ?? DateTimeOffset.Now;

        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest, 
            bool preserveUpgrade,
            bool useLockFile) 
            : this(appVersion, integrationTest, preserveUpgrade, useLockFile, "DtronixPackage.Tests")
        {
        }

        public DynamicPackage(
            Version appVersion,
            IntegrationTestBase integrationTest, 
            bool preserveUpgrade,
            bool useLockFile,
            string appName) 
            : base(appName, appVersion, preserveUpgrade, useLockFile)
        {
            _integrationTest = integrationTest;
            Logger = new NLogLogger(nameof(DynamicPackage));
        }

        protected override async Task<bool> OnOpen(bool isUpgrade)
        {
            try
            {
                if (Opening == null)
                    return true;

                return await Opening(this);
            }
            catch (Exception ex)
            {
                if (_integrationTest != null)
                {
                    _integrationTest.ThrowException = ex;
                    _integrationTest.TestComplete.Set();
                }

                throw;
            }
        }

        protected override async Task OnSave()
        {
            try
            {
                if(Saving != null)
                    await Saving(this);
            }
            catch (Exception ex)
            {
                if (_integrationTest != null)
                {
                    _integrationTest.ThrowException = ex;
                    _integrationTest.TestComplete.Set();
                }

                throw;
            }

        }

        protected override string OnTempFilePathRequest(string fileName)
        {
            var path = TempPackagePathRequest?.Invoke();

            return path;
        }
        
        public void ContentModifiedOverride()
        {
            DataModified();
        }
    }
}
