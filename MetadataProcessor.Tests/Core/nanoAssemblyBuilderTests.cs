//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

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
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == Enum.GetNames(typeof(NanoCLRTable)).Length,
                "Tables count from context doesn't match number of items in CLR Tables enum.");
        }
    }
}
