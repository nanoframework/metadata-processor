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
            Assert.IsFalse(nanoTablesContext.ClassNamesToExclude.Any());

            var oneClassOverAllTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllTypeDefinition(nanoTablesContext.AssemblyDefinition);
            var oneClassOverAllSubClassTypeDefinition = TestObjectHelper.GetTestNFAppOneClassOverAllSubClassTypeDefinition(nanoTablesContext.AssemblyDefinition);

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
