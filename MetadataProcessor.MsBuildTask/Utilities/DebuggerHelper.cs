// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace nanoFramework.Tools.Utilities
{
    internal static class DebuggerHelper
    {
        public static void WaitForDebuggerIfEnabled(string varName, TaskLoggingHelper logger, int timeoutSeconds = 30)
        {
            // this wait should be only available on debug build
            // to prevent unwanted wait on VS in machines where the variable is present
#if DEBUG
            TimeSpan timeoutToWaitForDebugToAttach = TimeSpan.FromSeconds(timeoutSeconds);

            var isToEnablePauseForDebug = Environment.GetEnvironmentVariable(varName, EnvironmentVariableTarget.User);

            if (!string.IsNullOrEmpty(isToEnablePauseForDebug)
                && isToEnablePauseForDebug.Equals("1", StringComparison.Ordinal))
            {
                var currentProcessId = Process.GetCurrentProcess().Id;
                var currentProcessName = Process.GetProcessById(currentProcessId).ProcessName;

                // output helper message to console, hopefully to be read by a human
                Console.WriteLine($".NET nanoFramework Metadata Processor msbuild task debugging is enabled. Waiting {timeoutSeconds} seconds for debugger to attach on Process Id: {currentProcessId} Name: {currentProcessName}...");

                logger.LogMessage(MessageImportance.Normal, $"Debugging of .NET nanoFramework Metadata Processor msbuild task is enabled. Waiting {timeoutSeconds} seconds for debugger attachment on Process Id: {currentProcessId} Name: {currentProcessName}...");

                // wait N seconds for debugger to attach
                while (!Debugger.IsAttached
                    && timeoutToWaitForDebugToAttach.TotalSeconds > 0)
                {
                    Thread.Sleep(1000);
                    timeoutToWaitForDebugToAttach -= TimeSpan.FromSeconds(1);
                }

                // stop debug
                Debugger.Break();
            }
#endif
        }
    }
}
