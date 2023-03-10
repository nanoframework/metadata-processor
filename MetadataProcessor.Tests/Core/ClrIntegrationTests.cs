using CliWrap;
using CliWrap.Buffered;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class CLRIntegrationTests
    {
        public static bool NanoClrIsInstalled { get; private set; } = false;

        [ClassInitialize]
        public static void InstallNanoClr(TestContext context)
        {
            Console.WriteLine("Install/upate nanoclr tool");

            var cmd = Cli.Wrap("dotnet")
                .WithArguments("tool update -g nanoclr")
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 1 minute
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromMinutes(1));

                var cliResult = cmd.ExecuteBufferedAsync(cts.Token).Task.Result;

                if (cliResult.ExitCode == 0)
                {
                    var regexResult = Regex.Match(cliResult.StandardOutput, @"((?>\(version ')(?'version'\d+\.\d+\.\d+)(?>'\)))");

                    if (regexResult.Success)
                    {
                        Console.WriteLine($"Install/update successful. Running v{regexResult.Groups["version"].Value}");

                        NanoClrIsInstalled = true;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to install/update nanoclr. {cliResult.StandardOutput}.");

                        NanoClrIsInstalled = false;
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to install/update nanoclr. Exit code {cliResult.ExitCode}.");
                    Console.WriteLine($"*** WON'T BE ABLE TO RUN UNITS TEST REQUIRING IT.");

                    NanoClrIsInstalled = false;
                }
            }
        }

        [TestMethod]
        public void RunBCLTest()
        {
            if(!NanoClrIsInstalled)
            {
                Assert.Inconclusive("nanoclr is not installed, can't run this test");
            }

            var workingDirectory = TestObjectHelper.GetTestNFAppLocation();
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
            if (!NanoClrIsInstalled)
            {
                Assert.Inconclusive("nanoclr is not installed, can't run this test");
            }

            // 5 seconds
            int runTimeout = 5000;

            var workingDirectory = TestObjectHelper.GetTestNFAppLocation();
            var mscorlibLocation = Path.Combine(workingDirectory, "mscorlib.pe");
            var nfTestAppLocation = TestObjectHelper.GetPathOfTestNFApp().Replace("exe", "pe");
            var nfTestClassLibLocation = TestObjectHelper.GetPathOfTestNFClassLib().Replace("dll", "pe");

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
                if (nanoCLR.Start())
                {
                    Console.WriteLine($"Running nanoCLR Win32 @ '{TestObjectHelper.GetNanoCLRLocation()}'");
                }
                else
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

                var outputOfTest = output.ToString();

                // look for standard messages
                Assert.IsTrue(outputOfTest.Contains("Ready."), $"Failed to find READY message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");
                Assert.IsTrue(outputOfTest.Contains("Done."), $"Failed to find DONE message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");
                Assert.IsTrue(outputOfTest.Contains("Exiting."), $"Failed to find EXITING message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");

                // look for any exceptions
                Assert.IsFalse(outputOfTest.Contains("++++ Exception "), $"Exception thrown by TestNFApp application.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");
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
