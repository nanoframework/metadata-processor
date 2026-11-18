// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class ClrIntegrationTests
    {
        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static Task InitializeNanoClr(TestContext context)
        {
            string instancePreviewSetting = Environment.GetEnvironmentVariable("NF_MDP_NANOCLR_INSTANCE_USE_PREVIEW");
            bool usePreviewClrInstance = false;

            if (string.IsNullOrEmpty(TestObjectHelper.NanoClrLocalInstance)
                && !string.IsNullOrEmpty(instancePreviewSetting))
            {
                Assert.IsTrue(
                    bool.TryParse(instancePreviewSetting, out usePreviewClrInstance),
                    "NF_MDP_NANOCLR_INSTANCE_USE_PREVIEW must be 'true' or 'false'.");
            }

            return InitializeNanoClr(context, usePreviewClrInstance);
        }

        public static async Task InitializeNanoClr(TestContext context, bool usePreviewClrInstance)
        {
            await ExecuteNanoClrAsync("--version", TimeSpan.FromSeconds(10), context);

            if (string.IsNullOrEmpty(TestObjectHelper.NanoClrLocalInstance))
            {
                string previewOption = usePreviewClrInstance ? " --preview" : string.Empty;
                await ExecuteNanoClrAsync($"instance --update{previewOption} -v diag", TimeSpan.FromMinutes(1), context);
            }
            else
            {
                context.WriteLine("Using local nanoCLR DLL: {0}", TestObjectHelper.NanoClrLocalInstance);
            }

            await ExecuteNanoClrAsync(
                $"instance --getversion {ComposeLocalClrInstancePath()} -v diag",
                TimeSpan.FromSeconds(10),
                context);
        }

        [TestMethod]
        public async Task RunBCLTest()
        {
            string mscorlibPeLocation = Path.Combine(TestObjectHelper.TestNFAppLocation, "mscorlib.pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {ComposeLocalClrInstancePath()} -v diag";

            string output = await ExecuteNanoClrAsync(arguments, TimeSpan.FromSeconds(30), TestContext);

            // look for any error message
            Assert.IsFalse(output.Contains("Error:"), $"Unexpected error message in output of NanoCLR.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

            // look for the error message reporting that there is no entry point
            Assert.IsTrue(output.Contains("Cannot find any entrypoint!"));
        }

        [TestMethod]
        public async Task RunTestNFAppTest()
        {
            string mscorlibPeLocation = Path.Combine(TestObjectHelper.TestNFAppLocation, "mscorlib.pe");
            string nfTestAppPeLocation = TestObjectHelper.NFAppFullPath.Replace("exe", "pe");
            string nfTestClassLibPeLocation = TestObjectHelper.TestNFClassLibFullPath.Replace("dll", "pe");

            // prepare launch of nanoCLR CLI
            string arguments = $"run --assemblies {mscorlibPeLocation} {nfTestAppPeLocation} {nfTestClassLibPeLocation} {ComposeLocalClrInstancePath()} -v diag";

            string output = await ExecuteNanoClrAsync(arguments, TimeSpan.FromSeconds(30), TestContext);

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

            // attribute declared on a property (MyClass1.MyField) has to be reachable through the getter
            Assert.IsTrue(output.Contains(">>>>>>> get_MyField has 'IgnoreAttribute' attribute"), $"Attribute declared on property wasn't cascaded to the getter.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

            // attributes declared on properties have to be reachable through the accessors, without duplicating
            // the ones declared on the accessor itself (see TestNFApp.PropertyAttributesTestClass)
            AssertAccessorAttributes(output, "OneClassOverAll", "get_DummyProperty", 1, 1);
            AssertAccessorAttributes(output, "OneClassOverAll", "set_DummyProperty", 1, 1);

            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "get_GetOnlyProperty", 1, 1);
            AssertAccessorNotPresent(output, "PropertyAttributesTestClass", "set_GetOnlyProperty");

            AssertAccessorNotPresent(output, "PropertyAttributesTestClass", "get_SetOnlyProperty");
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_SetOnlyProperty", 1, 1);

            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "get_PropertyWithSetterAttribute", 1, 1);
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_PropertyWithSetterAttribute", 1, 1);

            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "get_PropertyWithOtherAttributeOnSetter", 1, 0);
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_PropertyWithOtherAttributeOnSetter", 1, 1);

            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "get_SetterOnlyAttribute", 0, 0);
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_SetterOnlyAttribute", 1, 0);

            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "get_GetterOnlyAttribute", 1, 0);
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_GetterOnlyAttribute", 0, 0);

            AssertAccessorNotPresent(output, "PropertyAttributesTestClass", "get_SetOnlyPropertyWithSetterAttribute");
            AssertAccessorAttributes(output, "PropertyAttributesTestClass", "set_SetOnlyPropertyWithSetterAttribute", 1, 1);
        }

        private static async Task<string> ExecuteNanoClrAsync(string arguments, TimeSpan timeout, TestContext context)
        {
            context.WriteLine("Launching nanoclr {0}", arguments);

            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            Command cmd = Cli.Wrap("nanoclr")
                .WithArguments(arguments)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                {
                    output.AppendLine(line);
                    context.WriteLine("{0}", line);
                }))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                {
                    error.AppendLine(line);
                    context.WriteLine("[stderr] {0}", line);
                }))
                .WithValidation(CommandResultValidation.None);

            using CancellationTokenSource cts = new CancellationTokenSource(timeout);

            try
            {
                CommandResult result = await cmd.ExecuteAsync(cts.Token);

                context.WriteLine("nanoCLR exit code: {0}", result.ExitCode);
                Assert.AreEqual(
                    0,
                    result.ExitCode,
                    $"nanoclr {arguments} failed.{Environment.NewLine}{output}{Environment.NewLine}{error}");
            }
            catch (Win32Exception ex)
            {
                Assert.Fail($"Cannot start nanoclr: {ex.Message}. Install the tool before running tests: dotnet tool install --global nanoclr");
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"nanoclr {arguments} timed out after {timeout.TotalSeconds} seconds.{Environment.NewLine}{output}{Environment.NewLine}{error}");
            }

            return output.ToString();
        }

        private static void AssertAccessorAttributes(
            string output,
            string typeName,
            string accessorName,
            int expectedDummyCustomAttribute1Count,
            int expectedDummyCustomAttribute2Count)
        {
            Assert.IsTrue(
                output.Contains($"'{typeName}.{accessorName}' method has {expectedDummyCustomAttribute1Count + expectedDummyCustomAttribute2Count} custom attributes"),
                $"Unexpected number of attributes on {typeName}.{accessorName}.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

            Assert.IsTrue(
                output.Contains($">>>>>>> {typeName}.{accessorName} has 'DummyCustomAttribute1' attribute {expectedDummyCustomAttribute1Count} time(s)"),
                $"Expecting DummyCustomAttribute1 {expectedDummyCustomAttribute1Count} time(s) on {typeName}.{accessorName}.{Environment.NewLine}Output is:{Environment.NewLine}{output}");

            Assert.IsTrue(
                output.Contains($">>>>>>> {typeName}.{accessorName} has 'DummyCustomAttribute2' attribute {expectedDummyCustomAttribute2Count} time(s)"),
                $"Expecting DummyCustomAttribute2 {expectedDummyCustomAttribute2Count} time(s) on {typeName}.{accessorName}.{Environment.NewLine}Output is:{Environment.NewLine}{output}");
        }

        private static void AssertAccessorNotPresent(
            string output,
            string typeName,
            string accessorName)
        {
            Assert.IsTrue(
                output.Contains($"'{typeName}.{accessorName}' accessor not present"),
                $"{typeName}.{accessorName} wasn't expected to exist.{Environment.NewLine}Output is:{Environment.NewLine}{output}");
        }

        private static string ComposeLocalClrInstancePath()
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
    }
}
