using System;
using System.Linq;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Extensions
{
    [TestClass]
    public class TypeDefinitionExtensionsTests
    {
        [TestMethod]
        public void IncludeInStubTest()
        {
            var mscorlibAssemblyDefinition = TestObjectHelper.GetmscorlibAssemblyDefinition();

            var multicastDelegateTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == "System.Action");
            Assert.AreEqual("MulticastDelegate", multicastDelegateTypeDefinition.BaseType.Name);

            // test
            var r = multicastDelegateTypeDefinition.IncludeInStub();

            Assert.IsFalse(r);



            var classTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == "System.IO.Path");

            // test
            r = classTypeDefinition.IncludeInStub();

            Assert.IsTrue(r);


            
            var valueTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == "System.Int32");

            // test
            r = valueTypeDefinition.IncludeInStub();

            Assert.IsTrue(r);

            

            var interfaceTypeDefinition = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == "System.ICloneable");

            // test
            r = interfaceTypeDefinition.IncludeInStub();

            Assert.IsFalse(r);
        }

        [TestMethod]
        public void IsClassToExcludeTest()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            Assert.IsFalse(nanoTablesContext.ClassNamesToExclude.Any());

            var oneClassOverAllTypeDefinition = nanoTablesContext.TypeDefinitionTable.Items.First(i => i.FullName == "TestNFApp.OneClassOverAll");
            var oneClassOverAllSubClassTypeDefinition = nanoTablesContext.TypeDefinitionTable.Items.First(i => i.FullName == "TestNFApp.OneClassOverAll/SubClass");

            // test
            var r = oneClassOverAllTypeDefinition.IsClassToExclude();

            Assert.IsFalse(r);


            // test
            r = oneClassOverAllSubClassTypeDefinition.IsClassToExclude();

            Assert.IsFalse(r);



            // WARNING: leaking abstraction!!!!
            nanoTablesContext.ClassNamesToExclude.Add(oneClassOverAllTypeDefinition.FullName);



            // test
            r = oneClassOverAllTypeDefinition.IsClassToExclude();

            Assert.IsTrue(r);


            // test
            r = oneClassOverAllSubClassTypeDefinition.IsClassToExclude();

            Assert.IsTrue(r);
        }
    }
}
