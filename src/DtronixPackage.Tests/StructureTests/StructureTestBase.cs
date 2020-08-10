using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using DtronixPackage.Tests.IntegrationTests;
using NUnit.Framework;

namespace DtronixPackage.Tests.StructureTests
{
    public abstract class StructureTestBase
    {
        protected PackageBuilder PackageBuilder;
        protected DynamicPackage Package;
        
        public string PackageFilename { get; set; }


        [SetUp]
        public virtual Task Setup()
        {
            
            PackageBuilder = new PackageBuilder("saves");
            Package = new DynamicPackage(new Version(1, 0, 0, 0), null, true, true);
            PackageFilename = Path.Combine("saves/", Guid.NewGuid() + ".file");
            return Task.CompletedTask;
        }

    }
}