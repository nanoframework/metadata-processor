//
// Copyright (c) .NET Foundation and Contributors
// Portions Copyright (c) Microsoft Corporation.  All rights reserved.
// See LICENSE file in the project root for full license information.
//

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;
using System.Threading;

namespace nanoFramework.Tools.Utilities
{
    internal static class DebuggerHelper
    {
        public static void WaitForDebuggerIfEnabled(string varName, TaskLoggingHelper logger, int timeoutSeconds = 30)
        {

            // this wait should be only available on debug build
            // to prevent unwanted wait on VS in machines where the variable is present
#if DEBUG
            TimeSpan waitForDebugToAttach = TimeSpan.FromSeconds(timeoutSeconds);

            var debugEnabled = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);

            if (!string.IsNullOrEmpty(debugEnabled) && debugEnabled.Equals("1", StringComparison.Ordinal))
            {
                var currentProcessId = Process.GetCurrentProcess().Id;
                var currentProcessName = Process.GetProcessById(currentProcessId).ProcessName;

                logger.LogMessage(MessageImportance.Normal, $"Debugging of .NET nanoFramework Metadata Processor msbuild task is enabled. Waiting {timeoutSeconds} seconds for debugger attachment on Process Id: {currentProcessId} Name: {currentProcessName}...");

                // wait N seconds for debugger to attach
                while (!Debugger.IsAttached && waitForDebugToAttach.TotalSeconds > 0)
                {
                    Thread.Sleep(1000);
                    waitForDebugToAttach -= TimeSpan.FromSeconds(1);
                }

                Debugger.Break();
            }
#endif
        }
    }
}
