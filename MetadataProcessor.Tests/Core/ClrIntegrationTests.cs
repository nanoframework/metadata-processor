//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using CliWrap;
using CliWrap.Buffered;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
        public async Task RunBCLTest()
        {
            if (!NanoClrIsInstalled)
            {
                Assert.Inconclusive("nanoclr is not installed, can't run this test");
            }

            var mscorlibPeLocation = Path.Combine(TestObjectHelper.TestNFAppLocation, "mscorlib.pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {_localClrInstancePath} -v diag";

            Console.WriteLine($"Launching nanoclr with these arguments: '{arguments}'");

            var cmd = Cli.Wrap("nanoclr")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var cliResult = await cmd.ExecuteBufferedAsync(cts.Token);
                var exitCode = cliResult.ExitCode;
                // read standard output
                var output = cliResult.StandardOutput;

                if (exitCode == 0)
                {
                    // look for any error message 
                    Assert.IsFalse(output.Contains("Error:"), "Unexpected error message in output of NanoCLR");

                    // look for the error message reporting that there is no entry point
                    Assert.IsTrue(output.Contains("Cannot find any entrypoint!"));
                }
                else
                {
                    Assert.Fail($"nanoCLR ended with '{exitCode}' exit code.\r\n>>>>>>>>>>>>>\r\n{output}\r\n>>>>>>>>>>>>>");
                }
            }
        }

        [TestMethod]
        public async Task RunTestNFAppTest()
        {
            if (!NanoClrIsInstalled)
            {
                Assert.Inconclusive("nanoclr is not installed, can't run this test");
            }

            var workingDirectory = TestObjectHelper.NanoClrLocation;
            var mscorlibPeLocation = Path.Combine(workingDirectory, "mscorlib.pe");
            var nfTestAppPeLocation = TestObjectHelper.NFAppFullPath.Replace("exe", "pe");
            var nfTestClassLibPeLocation = TestObjectHelper.TestNFClassLibFullPath.Replace("dll", "pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {nfTestAppPeLocation} {nfTestClassLibPeLocation} {_localClrInstancePath} -v diag";

            Console.WriteLine($"Launching nanoclr with these arguments: '{arguments}'");

            var cmd = Cli.Wrap("nanoclr")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var cliResult = await cmd.ExecuteBufferedAsync(cts.Token);
                var exitCode = cliResult.ExitCode;
                // read standard output
                var output = cliResult.StandardOutput;

                if (exitCode == 0)
                {
                    // look for standard messages
                    Assert.IsTrue(output.Contains("Ready."), $"Failed to find READY message.{Environment.NewLine}Output is:{Environment.NewLine}{output}");
                    Assert.IsTrue(output.Contains("Done."), $"Failed to find DONE message.{Environment.NewLine}Output is:{Environment.NewLine}{output}");
                    Assert.IsTrue(output.Contains("Exiting."), $"Failed to find EXITING message.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

                    // look for any exceptions
                    Assert.IsFalse(output.Contains("++++ Exception "), $"Exception thrown by TestNFApp application.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

                    // look for any error message 
                    Assert.IsFalse(output.Contains("Error:"), "Unexpected error message in output of NanoCLR");

                    // look for the error message reporting that there is no entry point
                    Assert.IsFalse(output.Contains("Cannot find any entrypoint!"));
                }
                else
                {
                    Assert.Fail($"nanoCLR ended with '{exitCode}' exit code.\r\n>>>>>>>>>>>>>\r\n{output}\r\n>>>>>>>>>>>>>");
                }
            }
        }
    }
}
