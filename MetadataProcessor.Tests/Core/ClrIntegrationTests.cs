using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class ClrIntegrationTests
    {
        [TestMethod]
        public void RunBCLTest()
        {
            var workingDirectory = Path.GetDirectoryName(TestObjectHelper.GetTestNFAppLocation());
            var mscorlibLocation = Path.Combine(workingDirectory, "mscorlib.pe");

            // prepare the process start of the WIN32 nanoCLR
            Process nanoClr = new Process();
            
            // load only mscorlib
            string parameter = $"-load {mscorlibLocation}";

            nanoClr.StartInfo = new ProcessStartInfo(TestObjectHelper.GetNanoClrLocation(), parameter)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            // launch nanoCLR
            if (!nanoClr.Start())
            {
                Assert.Fail("Failed to start nanoCLR Win32");
            }

            // wait 5 seconds for exit
            nanoClr.WaitForExit(5000);

            Assert.IsTrue(nanoClr.HasExited, "nanoCLR hasn't completed execution");

            // read standard output
            var output = nanoClr.StandardOutput.ReadToEnd();

            // look for the error message reporting that there is no entry point
            Assert.IsTrue(output.Contains("Cannot find any entrypoint!"));
        }

        [TestMethod]
        public void RunTestNFAppTest()
        {
            // 5 seconds
            int runTimeout = 5000;

            var workingDirectory = Path.GetDirectoryName(TestObjectHelper.GetTestNFAppLocation());
            var mscorlibLocation = Path.Combine(workingDirectory, "mscorlib.pe");
            var nfTestAppLocation = TestObjectHelper.GetTestNFAppLocation().Replace("exe", "pe");
            var nfTestClassLibLocation = TestObjectHelper.GetTestNFClassLibLocation().Replace("dll", "pe");

            // prepare the process start of the WIN32 nanoCLR
            Process nanoClr = new Process();

            AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
            AutoResetEvent errorWaitHandle = new AutoResetEvent(false);

            try
            {
                // load only mscorlib
                string parameter = $"-load {nfTestAppLocation} -load {mscorlibLocation} -load {nfTestClassLibLocation}";

                nanoClr.StartInfo = new ProcessStartInfo(TestObjectHelper.GetNanoClrLocation(), parameter)
                {
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                // launch nanoCLR
                if (!nanoClr.Start())
                {
                    Assert.Fail("Failed to start nanoCLR Win32");
                }

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                nanoClr.OutputDataReceived += (sender, e) => {
                    if (e.Data == null)
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        output.AppendLine(e.Data);
                    }
                };
                nanoClr.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                    {
                        errorWaitHandle.Set();
                    }
                    else
                    {
                        error.AppendLine(e.Data);
                    }
                };

                nanoClr.Start();

                nanoClr.BeginOutputReadLine();
                nanoClr.BeginErrorReadLine();

                Console.WriteLine($"nanoCLR started @ process ID: {nanoClr.Id}");

                // wait for exit, no worries about the outcome
                nanoClr.WaitForExit(runTimeout);


                // look for standard messages
                Assert.IsTrue(output.ToString().Contains("Ready."), "Failed to find READY message.");
                Assert.IsTrue(output.ToString().Contains("Done."), "Failed to find DONE message.");
                Assert.IsTrue(output.ToString().Contains("Exiting."), "Failed to find EXITING message.");

                // look for any exceptions
                Assert.IsFalse(output.ToString().Contains("++++ Exception System.Exception"), "Exception thrown by TestNFApp application.");
            }
            finally
            {
                if (!nanoClr.HasExited)
                {
                    nanoClr.Kill();
                    nanoClr.WaitForExit(runTimeout);
                }
            }
        }
    }
}
