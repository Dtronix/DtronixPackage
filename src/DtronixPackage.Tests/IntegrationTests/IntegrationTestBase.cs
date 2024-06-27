using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace DtronixPackage.Tests.IntegrationTests
{
    public class IntegrationTestBase
    {
        protected class SampleJsonObj
        {
            public Guid Data { get; set; }
        }
        protected SampleJsonObj SampleJson;
        protected string SampleText;
        protected byte[] SampleByteArray;


        public ManualResetEventSlim TestComplete;
        public Exception ThrowException;
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        protected string ContentFileName;
        protected string PackageFilename;

        public IntegrationTestBase()
        {
            LogManager.ReconfigExistingLoggers();
            var consoleTarget = new ConsoleTarget("Console Target")
            {
                Layout = @"${time} ${longdate} ${uppercase: ${level}} ${logger} ${message} ${exception: format=ToString}"
            };

            var logConfig = new LoggingConfiguration();
            logConfig.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);

            // Override the manager target set by the AppInfo
            LogManager.Configuration = logConfig;

        }


        [OneTimeSetUp]
        public virtual void OneTimeSetup()
        {
            // Delete all existing test files before starting.
            if (!Directory.Exists("saves"))
                Directory.CreateDirectory("saves");

            var saveDirectory =  new DirectoryInfo("saves");

            try
            {
                foreach (var file in saveDirectory.GetFiles("*.file"))
                    file.Delete();

                foreach (var file in saveDirectory.GetFiles("*.lock"))
                    file.Delete();

                foreach (var file in saveDirectory.GetFiles("*.temp"))
                    file.Delete();
            }
            catch
            {
                // ignored
            }
        }
        [SetUp]
        public virtual void Setup()
        {
            ThrowException = null;
            TestComplete = new ManualResetEventSlim();

            SampleJson = new SampleJsonObj
            {
                Data = Guid.NewGuid()
            };

            SampleText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.";
            SampleByteArray = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};
            
            PackageFilename = Path.Combine("saves/", Guid.NewGuid() + ".file");
            ContentFileName = Guid.NewGuid() + ".dat";
        }

        [TearDown]
        public virtual void TearDown()
        {
            TestComplete?.Dispose();
        }

        protected void WaitTest(int milliseconds  = 500)
        {
            if (!TestComplete.Wait(milliseconds))
                throw new Exception($"Test did not complete within timeout period of {milliseconds}ms.");

            if (ThrowException != null)
                throw ThrowException;
        }

        
        protected async Task<DynamicPackage> CreateAndSavePackage(
            Func<PackageWriter, DynamicPackage<TestPackageContent>, Task> onSave, 
            Version appVersion = null)
        {

            if(appVersion == null)
                appVersion = new Version(1, 0);

            var file = new DynamicPackage(appVersion, this, false, false)
            {
                Writing = onSave
            };

            await file.Save(PackageFilename);
            return file;
        }
        
        protected async Task CreateAndClosePackage(Func<PackageWriter, DynamicPackage<TestPackageContent>, Task> onSave, Version appVersion = null)
        {
            var file = await CreateAndSavePackage(onSave, appVersion);
            file.Close();
        }

        protected async Task OpenWaitForCompletionPackage(Func<PackageReader, DynamicPackage<TestPackageContent>, Task<bool>> onOpen)
        {
            var file = new DynamicPackage(new Version(1,0), this, false, false)
            {
                Reading = async (writer, dynamicFile) =>
                {
                    var result = await onOpen(writer, dynamicFile);
                    TestComplete.Set();
                    return result;
                }
            };

            await file.Open(PackageFilename);
            file.Close();

            WaitTest(1000);
        }

        protected async Task OpenWaitForCompletionPackage(Func<PackageReader, DynamicPackage<TestPackageContent>, Task> onOpen)
        {
            var file = new DynamicPackage(new Version(1, 0), this, false, false)
            {
                Reading = async (reader, package) =>
                {
                    await onOpen.Invoke(reader, package);
                    TestComplete.Set();

                    return true;
                }
            };

            await file.Open(PackageFilename);
            file.Close();

            WaitTest(1000);
        }
    }
}