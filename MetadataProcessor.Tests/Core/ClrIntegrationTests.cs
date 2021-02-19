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
    public class CLRIntegrationTests
    {
        [TestMethod]
        public void RunBCLTest()
        {
            var workingDirectory = Path.GetDirectoryName(TestObjectHelper.GetTestNFAppLocation());
            var mscorlibLocation = Path.Combine(workingDirectory, "mscorlib.pe");

            // prepare the process start of the WIN32 nanoCLR
            Process nanoCLR = new Process();
            
            // load only mscorlib
            string parameter = $"-load {mscorlibLocation}";

            nanoCLR.StartInfo = new ProcessStartInfo(TestObjectHelper.GetNanoCLRLocation(), parameter)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            // launch nanoCLR
            if (!nanoCLR.Start())
            {
                Assert.Fail("Failed to start nanoCLR Win32");
            }

            // wait 5 seconds for exit
            nanoCLR.WaitForExit(5000);

            Assert.IsTrue(nanoCLR.HasExited, "nanoCLR hasn't completed execution");

            // read standard output
            var output = nanoCLR.StandardOutput.ReadToEnd();
            
            // look for any error message 
            Assert.IsFalse(output.Contains("Error:"), "Unexpected error message in output of NanoCLR");

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
            Process nanoCLR = new Process();

            AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
            AutoResetEvent errorWaitHandle = new AutoResetEvent(false);

            try
            {
                // load only mscorlib
                string parameter = $"-load {nfTestAppLocation} -load {mscorlibLocation} -load {nfTestClassLibLocation}";

                nanoCLR.StartInfo = new ProcessStartInfo(TestObjectHelper.GetNanoCLRLocation(), parameter)
                {
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                // launch nanoCLR
                if (!nanoCLR.Start())
                {
                    Assert.Fail("Failed to start nanoCLR Win32");
                }

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                nanoCLR.OutputDataReceived += (sender, e) => {
                    if (e.Data == null)
                    {
                        outputWaitHandle.Set();
                    }
                    else
                    {
                        output.AppendLine(e.Data);
                    }
                };
                nanoCLR.ErrorDataReceived += (sender, e) =>
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

                nanoCLR.Start();

                nanoCLR.BeginOutputReadLine();
                nanoCLR.BeginErrorReadLine();

                Console.WriteLine($"nanoCLR started @ process ID: {nanoCLR.Id}");

                // wait for exit, no worries about the outcome
                nanoCLR.WaitForExit(runTimeout);


                // look for standard messages
                Assert.IsTrue(output.ToString().Contains("Ready."), "Failed to find READY message.");
                Assert.IsTrue(output.ToString().Contains("Done."), "Failed to find DONE message.");
                Assert.IsTrue(output.ToString().Contains("Exiting."), "Failed to find EXITING message.");

                // look for any exceptions
                Assert.IsFalse(output.ToString().Contains("++++ Exception System.Exception"), "Exception thrown by TestNFApp application.");
            }
            finally
            {
                if (!nanoCLR.HasExited)
                {
                    nanoCLR.Kill();
                    nanoCLR.WaitForExit(runTimeout);
                }
            }
        }
    }
}
