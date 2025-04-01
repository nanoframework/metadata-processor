// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoMethodDefinitionTableTests
    {
        private TypeDefinition _testDelegatesClassTypeDefinition;
        private TypeDefinition _destructorTestClassTypeDefinition;
        private TypeDefinition _destructorAnotherTestClassTypeDefinition;
        private TypeDefinition _destructorAnotherBaseClassTypeDefinition;

        [TestInitialize]
        public void Setup()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            _testDelegatesClassTypeDefinition = TestObjectHelper.GetTestNFAppTestingDelegatesTypeDefinition(nanoTablesContext.AssemblyDefinition);
            _destructorTestClassTypeDefinition = TestObjectHelper.GetTestNFAppDestructorsTestClassTypeDefinition(nanoTablesContext.AssemblyDefinition);
            _destructorAnotherTestClassTypeDefinition = TestObjectHelper.GetTestNFAppDestructorsTestAnotherClassTypeDefinition(nanoTablesContext.AssemblyDefinition);
            _destructorAnotherBaseClassTypeDefinition = TestObjectHelper.GetTestNFAppDestructorsTestAnotherClassBaseTypeDefinition(nanoTablesContext.AssemblyDefinition);
        }

        #region delegate method flags

        [TestMethod]
        public void TestDelegateInvokeMethodReturnsDelegateInvokeFlag()
        {
            var methodDefinition = TestObjectHelper.GetMethodDefinition(_testDelegatesClassTypeDefinition, "SimpleDelegate", "Invoke");

            uint flags = nanoMethodDefinitionTable.GetFlags(methodDefinition);

            // Assert
            const uint expectedFlag = 0x00020000; // MD_DelegateInvoke
            Assert.AreEqual(expectedFlag, flags & expectedFlag);
        }

        [TestMethod]
        public void TestDelegateConstructorMethodReturnsDelegateConstructorFlag()
        {
            var methodDefinition = TestObjectHelper.GetMethodDefinition(_testDelegatesClassTypeDefinition, "SimpleDelegate", ".ctor");

            uint flags = nanoMethodDefinitionTable.GetFlags(methodDefinition);

            // Assert
            const uint expectedFlag = 0x00010000; // MD_DelegateConstructor
            Assert.IsTrue((flags & expectedFlag) == expectedFlag, "Expected flag not set for Delegate constructor.");
        }

        [TestMethod]
        public void TestDelegateBeginInvokeMethodReturnsDelegateBeginInvokeFlag()
        {
            // Arrange
            var methodDefinition = TestObjectHelper.GetMethodDefinition(_testDelegatesClassTypeDefinition, "SimpleDelegate", "BeginInvoke");

            // Act
            uint flags = nanoMethodDefinitionTable.GetFlags(methodDefinition);

            // Assert
            const uint expectedFlag = 0x00040000; // MD_DelegateBeginInvoke
            Assert.IsTrue((flags & expectedFlag) == expectedFlag, "Expected flag not set for BeginInvoke method.");
        }

        [TestMethod]
        public void TestDelegateEndInvokeMethodReturnsDelegateEndInvokeFlag()
        {
            // Arrange
            var methodDefinition = TestObjectHelper.GetMethodDefinition(_testDelegatesClassTypeDefinition, "SimpleDelegate", "EndInvoke");

            // Act
            uint flags = nanoMethodDefinitionTable.GetFlags(methodDefinition);

            // Assert
            const uint expectedFlag = 0x00080000; // MD_DelegateEndInvoke
            Assert.IsTrue((flags & expectedFlag) == expectedFlag, "Expected flag not set for EndInvoke method.");
        }

        #endregion

        #region finalizer method flags

        [DataRow("DestructorsTestClass")]
        [DataRow("DestructorsTestAnotherClass")]
        [DataRow("DestructorsTestAnotherClassBase")]
        [TestMethod]
        public void TestFinalizerMethodReturnsFinalizerFlag(string className)
        {
            // Arrange
            MethodDefinition methodDefinition = null;

            if (className == "DestructorsTestClass")
            {
                methodDefinition = _destructorTestClassTypeDefinition.Methods.First(m => m.Name == "Finalize");
            }
            else if (className == "DestructorsTestAnotherClass")
            {
                methodDefinition = _destructorAnotherTestClassTypeDefinition.Methods.First(m => m.Name == "Finalize");
            }
            else if (className == "DestructorsTestAnotherClassBase")
            {
                methodDefinition = _destructorAnotherBaseClassTypeDefinition.Methods.First(m => m.Name == "Finalize");
            }
            else
            {
                Assert.Fail("Invalid class name.");
            }

            Assert.IsNotNull(methodDefinition, "Finalizer method not found.");

            // Act
            uint flags = nanoMethodDefinitionTable.GetFlags(methodDefinition);

            // Assert
            const uint expectedFlag = 0x00004000; // MD_Finalizer
            Assert.IsTrue((flags & expectedFlag) == expectedFlag, "Expected flag not set for Finalizer method.");
        }

        #endregion
    }
}
