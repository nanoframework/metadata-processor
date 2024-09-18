//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoMethodDefinitionTableTests
    {
        private TypeDefinition _testDelegatesClassTypeDefinition;

        [TestInitialize]
        public void Setup()
        {
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();
            _testDelegatesClassTypeDefinition = TestObjectHelper.GetTestNFAppTestingDelegatesTypeDefinition(nanoTablesContext.AssemblyDefinition);
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
    }
}
