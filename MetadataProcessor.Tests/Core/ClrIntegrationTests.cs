using CliWrap;
using CliWrap.Buffered;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class CLRIntegrationTests
    {
        private static string _localClrInstancePath = $" --localinstance \"E:\\GitHub\\nf-interpreter\\build\\bin\\Debug\\net6.0\\NanoCLR\\nanoFramework.nanoCLR.dll";
        
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

            var testAppExecutable = TestObjectHelper.GetPathOfTestNFApp();
            var appPeLocation = testAppExecutable.Replace(".exe", ".pe");
            var mscorlibPeLocation = Path.Combine(TestObjectHelper.GetTestNFAppLocation(), "mscorlib.pe");

            // prepare launch of nanoCLR CLI

            var cmd = Cli.Wrap("nanoclr")
                .WithArguments($"run --assemblies {mscorlibPeLocation} {appPeLocation} {_localClrInstancePath}")
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var cliResult = cmd.ExecuteBufferedAsync(cts.Token).Task.Result;

                if (cliResult.ExitCode == 0)
                {
                    // read standard output
                    var output = cliResult.StandardOutput;

                    // look for any error message 
                    Assert.IsFalse(output.Contains("Error:"), "Unexpected error message in output of NanoCLR");

                    // look for the error message reporting that there is no entry point
                    Assert.IsTrue(output.Contains("Cannot find any entrypoint!"));
                }
                else
                {
                    Assert.Fail("nanoCLR hasn't completed execution");
                }
            }    
        }

        [TestMethod]
        public void RunTestNFAppTest()
        {
            if (!NanoClrIsInstalled)
            {
                Assert.Inconclusive("nanoclr is not installed, can't run this test");
            }

            var workingDirectory = TestObjectHelper.GetTestNFAppLocation();
            var mscorlibPeLocation = Path.Combine(workingDirectory, "mscorlib.pe");
            var nfTestAppPeLocation = TestObjectHelper.GetPathOfTestNFApp().Replace("exe", "pe");
            var nfTestClassLibPeLocation = TestObjectHelper.GetPathOfTestNFClassLib().Replace("dll", "pe");

            // prepare launch of nanoCLR CLI
            var cmd = Cli.Wrap("nanoclr")
                .WithArguments($"instance -assemblies {mscorlibPeLocation} {nfTestAppPeLocation} {nfTestClassLibPeLocation} {_localClrInstancePath}")
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var cliResult = cmd.ExecuteBufferedAsync(cts.Token).Task.Result;

                if (cliResult.ExitCode == 0)
                {
                    // read standard output
                    var outputOfTest = cliResult.StandardOutput;

                    // look for standard messages
                    Assert.IsTrue(outputOfTest.Contains("Ready."), $"Failed to find READY message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");
                    Assert.IsTrue(outputOfTest.Contains("Done."), $"Failed to find DONE message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");
                    Assert.IsTrue(outputOfTest.Contains("Exiting."), $"Failed to find EXITING message.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");

                    // look for any exceptions
                    Assert.IsFalse(outputOfTest.Contains("++++ Exception "), $"Exception thrown by TestNFApp application.{Environment.NewLine}Output is:{Environment.NewLine}{outputOfTest}");

                    // look for any error message 
                    Assert.IsFalse(outputOfTest.Contains("Error:"), "Unexpected error message in output of NanoCLR");

                    // look for the error message reporting that there is no entry point
                    Assert.IsTrue(outputOfTest.Contains("Cannot find any entrypoint!"));
                }
                else
                {
                    Assert.Fail("nanoCLR hasn't completed execution");
                }
            }
        }
    }
}
