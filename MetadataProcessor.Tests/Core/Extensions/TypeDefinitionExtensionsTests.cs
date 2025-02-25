//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Linq;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;
using System.IO;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Extensions
{
    [TestClass]
    public class TypeDefinitionExtensionsTests
    {
        [TestMethod]
        public void IncludeInStubTest()
        {
            var mscorlibAssemblyDefinition = TestObjectHelper.GetmscorlibAssemblyDefinition();

            var multicastDelegateTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == typeof(System.Action).FullName);
            Assert.AreEqual(typeof(MulticastDelegate).Name, multicastDelegateTypeDefinition.BaseType.Name);

            // test
            var r = multicastDelegateTypeDefinition.IncludeInStub();

            Assert.IsFalse(r);



            var classTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == typeof(System.IO.Path).FullName);

            // test
            r = classTypeDefinition.IncludeInStub();

            Assert.IsTrue(r);



            var valueTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == typeof(System.Int32).FullName);

            // test
            r = valueTypeDefinition.IncludeInStub();

            Assert.IsTrue(r);



            var interfaceTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == typeof(System.ICloneable).FullName);

            // test
            r = interfaceTypeDefinition.IncludeInStub();

            Assert.IsFalse(r);
        }

        [TestMethod]
        public void IsClassToExcludeTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var oneClassOverAllSubClassTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllSubClassTypeDefinition(nanoTablesContext.AssemblyDefinition);

            // test
            var r = oneClassOverAllTypeDefinition.IsToExclude();

            Assert.IsFalse(r);


            // test
            r = oneClassOverAllSubClassTypeDefinition.IsToExclude();

            Assert.IsFalse(r);



            // WARNING: leaking abstraction!!!!
            nanoTablesContext.ClassNamesToExclude.Add(oneClassOverAllTypeDefinition.FullName);



            // test
            r = oneClassOverAllTypeDefinition.IsToExclude();

            Assert.IsTrue(r);


            // test
            r = oneClassOverAllSubClassTypeDefinition.IsToExclude();

            Assert.IsTrue(r);
        }

        [TestMethod]
        public void TypesInLibraryToExcludeTests()
        {
            // Arrange
            string libLocation = Path.Combine(
                TestObjectHelper.TestExecutionLocation,
                "TestNFClassLibrary");

            string libAssemblyLocation = Path.Combine(
                libLocation,
               "TestNFClassLibrary.dll");

            AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(libAssemblyLocation);

            nanoTablesContext nanoTablesContext = new nanoTablesContext(
                assemblyDefinition,
                null,
                // adding this here, equivalent to the NFMDP_PE_ExcludeClassByName project property
                new List<string>() { "TestNFClassLibrary.IAmATypeToExclude" },
                null,
                false,
                false,
                false);

            ModuleDefinition testNFLibraryModule = nanoTablesContext.AssemblyDefinition.Modules[0];

            // Assert

            // test if <TestNFClassLibrary.ClassOnAnotherAssembly> type is present
            Assert.IsNotNull(testNFLibraryModule.Types.FirstOrDefault(i => i.FullName == "TestNFClassLibrary.ClassOnAnotherAssembly"), "TestNFClassLibrary.ClassOnAnotherAssembly type not found");

            // test if <TestNFClassLibrary.IAmATypeToExclude> type is present
            Assert.IsNotNull(testNFLibraryModule.Types.FirstOrDefault(i => i.FullName == "TestNFClassLibrary.IAmATypeToExclude"), "TestNFClassLibrary.IAmATypeToExclude type not found");

            // test if <TestNFClassLibrary.IAmATypeToExclude> type is to be excluded
            // this is being excluded with the NFMDP_PE_ExcludeClassByName project property
            Assert.IsTrue(testNFLibraryModule.Types.First(i => i.FullName == "TestNFClassLibrary.IAmATypeToExclude").IsToExclude(), "TestNFClassLibrary.IAmATypeToExclude type is not listed to be excluded, when it should");

            // test if <TestNFClassLibrary.IAmAlsoATypeToExclude> type is present
            Assert.IsNotNull(testNFLibraryModule.Types.FirstOrDefault(i => i.FullName == "TestNFClassLibrary.IAmAlsoATypeToExclude"), "TestNFClassLibrary.IAmAlsoATypeToExclude type not found");

            // test if <TestNFClassLibrary.IAmAlsoATypeToExclude> type is to be excluded
            // this is being excluded with the ExcludeType attribute
            Assert.IsTrue(testNFLibraryModule.Types.First(i => i.FullName == "TestNFClassLibrary.IAmAlsoATypeToExclude").IsToExclude(), "TestNFClassLibrary.IAmAlsoATypeToExclude type is not listed to be excluded, when it should");

            // test if <TestNFClassLibrary.IAmAnEnumToExclude> type is present
            Assert.IsNotNull(testNFLibraryModule.Types.FirstOrDefault(i => i.FullName == "TestNFClassLibrary.IAmAnEnumToExclude"), "TestNFClassLibrary.IAmAnEnumToExclude type not found");

            // test if <TestNFClassLibrary.IAmAnEnumToExclude> type is to be excluded
            // this is being excluded with the ExcludeType attribute
            Assert.IsTrue(testNFLibraryModule.Types.First(i => i.FullName == "TestNFClassLibrary.IAmAnEnumToExclude").IsToExclude(), "TestNFClassLibrary.IAmAnEnumToExclude type is not listed to be excluded, when it should");
        }

        [TestMethod]
        public void DefaultTypesToExcludeTests()
        {
            // Arrange
            AssemblyDefinition mscorlibAssemblyDefinition = AssemblyDefinition.ReadAssembly(TestObjectHelper.MscorlibFullPath);

            nanoTablesContext context = new nanoTablesContext(
                mscorlibAssemblyDefinition,
                null,
                new List<string>(),
                null,
                false,
                false,
                true);

            ModuleDefinition mscorlibModule = context.AssemblyDefinition.Modules[0];

            // Assert
            Assert.IsNotNull(context);

            Assert.IsTrue(mscorlibModule.Types.First(i => i.FullName == "ThisAssembly").IsToExclude(), "ThisAssembly type is not listed to be excluded, when it should");
        }
    }
}
