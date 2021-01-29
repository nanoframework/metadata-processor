﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                nanoAssemblyBuilder.GetTables(nanoTablesContext).Count() == Enum.GetNames(typeof(ClrTable)).Length,
                "Tables count from context doesn't match number of items in ClrTables enum.");
        }
    }
}