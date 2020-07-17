using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace DtronixPackage.Tests
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

        }

        protected async Task AssertFileExistWithin(string path, int timeout = 5000)
        {
            Assert.IsTrue(await FileExistWithin(path, timeout), $"File {path} does not exist.");
        }

        protected async Task AssertFileDoesNotExistWithin(string path, int timeout = 100)
        {
            Assert.IsFalse(await FileExistWithin(path, timeout), $"File {path} exists.");
        }

        protected async Task<bool> FileExistWithin(string path, int timeout)
        {
            Logger.Trace("FileExistWithin(path: {0}, timeout: {1})", path, timeout);
            if (File.Exists(path))
            {
                Logger.Trace("File exists: {0}", path);
                return true;
            }


            var tcs = new TaskCompletionSource<bool>();

            var absolutePath = Path.GetFullPath(path);
            using var watcher = new FileSystemWatcher(Path.GetDirectoryName(absolutePath), Path.GetFileName(path));
            watcher.Changed += (sender, args) =>
            {
                var fileExists = File.Exists(absolutePath);
                Logger.Trace("Directory changed [{0}]:{1}; Exists:{2}", args.ChangeType, absolutePath, fileExists);
                tcs.SetResult(fileExists);
            };

            watcher.EnableRaisingEvents = true;

            // Check once again to possibly shortcut watching.
            if (File.Exists(path))
            {
                Logger.Trace("Hot Path file exists: {0}", path);
                watcher.EnableRaisingEvents = false;
                return true;
            }

            if (Task.WaitAny(new Task[] {tcs.Task}, timeout) == 0)
            {
                return await tcs.Task;
            }

            Logger.Trace("Did not detect changes to file: {0}", path);
            // Try one last time after the timeout above.
            return File.Exists(path);
        }

        
        protected void AssertFileIsReadOnly(string path)
        {
            Assert.Multiple(() =>
            {
                Assert.Throws<IOException>(() => new FileStream(path, FileMode.Create));
                Assert.Throws<IOException>(() => File.Delete(path));
                Assert.DoesNotThrow(() => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Close());
            });
        }

        protected void WaitTest(int milliseconds  = 500)
        {
            if (!TestComplete.Wait(milliseconds))
                throw new Exception($"Test did not complete within timeout period of {milliseconds}ms.");

            if (ThrowException != null)
                throw ThrowException;
        }

        
        protected async Task<DynamicPackage> CreateAndSavePackage(
            Func<DynamicPackage, Task> onSave, 
            Version appVersion = null)
        {

            if(appVersion == null)
                appVersion = new Version(1, 0);

            var file = new DynamicPackage(appVersion, this, false, false)
            {
                Saving = onSave
            };

            await file.Save(PackageFilename);
            return file;
        }
        
        protected async Task CreateAndClosePackage(Func<DynamicPackage, Task> onSave, Version appVersion = null)
        {
            var file = await CreateAndSavePackage(onSave, appVersion);
            file.Close();
        }

        protected async Task OpenWaitForCompletionPackage(Func<DynamicPackage, Task<bool>> onOpen)
        {
            var file = new DynamicPackage(new Version(1,0), this, false, false)
            {
                Opening = async dynamicFile =>
                {
                    var result = await onOpen(dynamicFile);
                    TestComplete.Set();
                    return result;
                }
            };

            await file.Open(PackageFilename);
            file.Close();

            WaitTest(1000);
        }

        protected async Task OpenWaitForCompletionPackage(Func<DynamicPackage, Task> onOpen)
        {
            var file = new DynamicPackage(new Version(1,0), this, false, false);
            file.Opening = async argFile =>
            {
                await onOpen.Invoke(file);
                TestComplete.Set();

                return true;
            };

            await file.Open(PackageFilename);
            file.Close();

            WaitTest(1000);
        }
    }
}