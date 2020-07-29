using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DtronixPackage.Tests
{
    public class DynamicPackage : Package<PackageContent>
    {
        private readonly IntegrationTestBase _integrationTest;

        public Func<DynamicPackage, Task<bool>> Opening;

        public Func<DynamicPackage, Task> Saving;

        public Func<string> TempPackagePathRequest;

        public List<PackageUpgrade> UpgradeOverrides => Upgrades;

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
                _integrationTest.ThrowException = ex;
                _integrationTest.TestComplete.Set();
            }
            
            return false;
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
                _integrationTest.ThrowException = ex;
                _integrationTest.TestComplete.Set();
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
