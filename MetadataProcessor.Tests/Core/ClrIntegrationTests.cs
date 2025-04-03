// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class ClrIntegrationTests
    {
        public static bool NanoClrIsInstalled { get; private set; } = false;

        [ClassInitialize]
        public static void InstallNanoClr(TestContext context)
        {
            Console.WriteLine("Install/upate nanoclr tool");

            // get installed tool version (if installed)
            Command cmd = Cli.Wrap("nanoclr")
                .WithArguments("--help")
                .WithValidation(CommandResultValidation.None);

            bool performInstallUpdate = false;

            // setup cancellation token with a timeout of 10 seconds
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                BufferedCommandResult cliResult = cmd.ExecuteBufferedAsync(cts.Token).Task.Result;

                if (cliResult.ExitCode == 0)
                {
                    Match regexResult = Regex.Match(cliResult.StandardOutput, @"(?'version'\d+\.\d+\.\d+)", RegexOptions.RightToLeft);

                    if (regexResult.Success)
                    {
                        Console.WriteLine($"Running nanoclr v{regexResult.Groups["version"].Value}");

                        // compose version
                        Version installedVersion = new Version(regexResult.Groups[1].Value);

                        NanoClrIsInstalled = true;
                        string responseContent = null;

                        // check latest version
                        using (HttpClient client = new HttpClient())
                        {
                            try
                            {
                                // Set the user agent string to identify the client.
                                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36");

                                // Set any additional headers, if needed.
                                client.DefaultRequestHeaders.Add("Accept", "application/json");

                                // Set the URL to request.
                                string url = "https://api.nuget.org/v3-flatcontainer/nanoclr/index.json";

                                // Make the HTTP request and retrieve the response.
                                responseContent = client.GetStringAsync(url).Result;
                            }
                            catch (HttpRequestException e)
                            {
                                // Handle any exceptions that occurred during the request.
                                Console.WriteLine(e.Message);
                            }
                        }

                        NuGetPackage package = JsonConvert.DeserializeObject<NuGetPackage>(responseContent);
                        Version latestPackageVersion = new Version(package.Versions[package.Versions.Length - 1]);

                        // check if we are running the latest one
                        if (latestPackageVersion > installedVersion)
                        {
                            // need to update
                            performInstallUpdate = true;
                        }
                        else
                        {
                            Console.WriteLine($"No need to update. Running v{latestPackageVersion}");

                            performInstallUpdate = false;
                        }
                    }
                    else
                    {
                        // something wrong with the output, can't proceed
                        Console.WriteLine("Failed to parse current nanoCLR CLI version!");
                    }
                }
                else
                {
                    NanoClrIsInstalled = false;

                    // give it a try by forcing the install
                    performInstallUpdate = true;
                }
            }
            catch (Win32Exception)
            {
                // nanoclr doesn't seem to be installed
                performInstallUpdate = true;
                NanoClrIsInstalled = false;
            }

            if (performInstallUpdate)
            {
                cmd = Cli.Wrap("dotnet")
                .WithArguments("tool update -g nanoclr")
                .WithValidation(CommandResultValidation.None);

                // setup cancellation token with a timeout of 1 minute
                using (var cts1 = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    BufferedCommandResult cliResult = cmd.ExecuteBufferedAsync(cts1.Token).Task.Result;

                    if (cliResult.ExitCode == 0)
                    {
                        // this will be either (on update): 
                        // Tool 'nanoclr' was successfully updated from version '1.0.205' to version '1.0.208'.
                        // or (update becoming reinstall with same version, if there is no new version):
                        // Tool 'nanoclr' was reinstalled with the latest stable version (version '1.0.208').
                        Match regexResult = Regex.Match(cliResult.StandardOutput, @"((?>version ')(?'version'\d+\.\d+\.\d+)(?>'))");

                        if (regexResult.Success)
                        {
                            Console.WriteLine($"Install/update successful. Running v{regexResult.Groups["version"].Value}");

                            NanoClrIsInstalled = true;
                        }
                        else
                        {
                            Console.WriteLine($"*** Failed to install/update nanoclr *** {Environment.NewLine} {cliResult.StandardOutput}");

                            NanoClrIsInstalled = false;
                        }
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Failed to install/update nanoclr. Exit code {cliResult.ExitCode}."
                            + Environment.NewLine
                            + Environment.NewLine
                            + "****************************************"
                            + Environment.NewLine
                            + "*** WON'T BE ABLE TO RUN UNITS TESTS ***"
                            + Environment.NewLine
                            + "****************************************");

                        NanoClrIsInstalled = false;
                    }
                }
            }

            if (!string.IsNullOrEmpty(TestObjectHelper.NanoClrLocalInstance))
            {
                // done here as we are using a local instance of nanoCLR DLL
            }
            else
            {
                Console.WriteLine("Upate nanoCLR instance");

                // update nanoCLR instance
                string arguments = "instance --update";

                cmd = Cli.Wrap("nanoclr")
                    .WithArguments(arguments)
                    .WithValidation(CommandResultValidation.None);

                using CancellationTokenSource updateCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

                BufferedCommandResult cliResult = cmd.ExecuteBufferedAsync(updateCts.Token).Task.Result;

                if (cliResult.ExitCode == 0)
                {
                    // this will be either (on update): 
                    // Updated to v1.8.1.102
                    // or (on same version):
                    // Already at v1.8.1.102
                    Match regexResult = Regex.Match(cliResult.StandardOutput, @"((?>version )(?'version'\d+\.\d+\.\d+))");

                    if (regexResult.Success)
                    {
                        Console.WriteLine($"Install/update successful. Running v{regexResult.Groups["version"].Value}");
                    }
                    else
                    {
                        Console.WriteLine($"*** Failed to update nanoCLR instance ***");
                        Console.WriteLine($"\r\nExit code {cliResult.ExitCode}. \r\nOutput: {Environment.NewLine} {cliResult.StandardOutput}");
                    }
                }
                else
                {
                    Console.WriteLine($"*** Failed to update nanoCLR instance ***");
                    Console.WriteLine($"\r\nExit code {cliResult.ExitCode}. \r\nOutput: {Environment.NewLine} {cliResult.StandardOutput}");
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

            string mscorlibPeLocation = Path.Combine(TestObjectHelper.TestNFAppLocation, "mscorlib.pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {ComposeLocalClrInstancePath()} -v diag";

            Console.WriteLine($"Launching nanoclr with these arguments: '{arguments}'");

            Command cmd = Cli.Wrap("nanoclr")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                BufferedCommandResult cliResult = await cmd.ExecuteBufferedAsync(cts.Token);
                int exitCode = cliResult.ExitCode;
                // read standard output
                string output = cliResult.StandardOutput;

                if (exitCode == 0)
                {
                    // look for any error message 
                    Assert.IsFalse(output.Contains("Error:"), "Unexpected error message in output of NanoCLR");

                    // look for the error message reporting that there is no entry point
                    Assert.IsTrue(output.Contains("Cannot find any entrypoint!"));

                    Console.WriteLine($"\r\n>>>>>>>>>>>>>\r\n{output}\r\n>>>>>>>>>>>>>");
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

            string mscorlibPeLocation = Path.Combine(TestObjectHelper.TestNFAppLocation, "mscorlib.pe");
            string nfTestAppPeLocation = TestObjectHelper.NFAppFullPath.Replace("exe", "pe");
            string nfTestClassLibPeLocation = TestObjectHelper.TestNFClassLibFullPath.Replace("dll", "pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {nfTestAppPeLocation} {nfTestClassLibPeLocation} {ComposeLocalClrInstancePath()} -v diag";

            Console.WriteLine($"Launching nanoclr with these arguments: '{arguments}'");

            Command cmd = Cli.Wrap("nanoclr")
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None);

            // setup cancellation token with a timeout of 5 seconds
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                BufferedCommandResult cliResult = await cmd.ExecuteBufferedAsync(cts.Token);
                int exitCode = cliResult.ExitCode;
                // read standard output
                string output = cliResult.StandardOutput;

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

                    Console.WriteLine($"\r\n>>>>>>>>>>>>>\r\n{output}\r\n>>>>>>>>>>>>>");
                }
                else
                {
                    Assert.Fail($"nanoCLR ended with '{exitCode}' exit code.\r\n>>>>>>>>>>>>>\r\n{output}\r\n>>>>>>>>>>>>>");
                }
            }
        }

        private string ComposeLocalClrInstancePath()
        {
            StringBuilder arguments = new StringBuilder(" --localinstance");

            if (string.IsNullOrEmpty(TestObjectHelper.NanoClrLocalInstance))
            {
                return null;
            }
            else
            {
                arguments.Append($" \"{TestObjectHelper.NanoClrLocalInstance}\"");
            }

            return arguments.ToString();
        }

        internal class NuGetPackage
        {
            public string[] Versions { get; set; }
        }
    }
}
