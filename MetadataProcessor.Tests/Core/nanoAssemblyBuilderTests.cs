// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core
{
    [TestClass]
    public class nanoAssemblyBuilderTests
    {
        [TestMethod]
        public void AssemblyTablesCountTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            Assert.IsTrue(
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == nanoAssemblyBuilder.TablesCount,
                "Tables count from context doesn't match the nanoAssemblyBuilder.TablesCount property.");

            Assert.IsTrue(
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == Enum.GetNames(typeof(NanoClrTable)).Length,
                "Tables count from context doesn't match number of items in CLR Tables enum.");
        }
    }
}
