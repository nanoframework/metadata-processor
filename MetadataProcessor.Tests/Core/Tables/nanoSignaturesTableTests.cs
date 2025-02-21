// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using TypeDefinition = Mono.Cecil.TypeDefinition;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables
{
    [TestClass]
    public class nanoSignaturesTableTests
    {
        [DataRow("System.DateTime", NanoCLRDataType.DATATYPE_DATETIME)]
        [DataRow("System.TimeSpan", NanoCLRDataType.DATATYPE_TIMESPAN)]
        [DataRow("System.String", NanoCLRDataType.DATATYPE_STRING)]
        [DataRow("System.Object", NanoCLRDataType.DATATYPE_CLASS)]
        [DataRow("System.IntPtr", NanoCLRDataType.DATATYPE_VALUETYPE)]
        [DataRow("System.WeakReference", NanoCLRDataType.DATATYPE_WEAKCLASS)]
        [TestMethod]
        public void WriteDataTypeForTypeRef_ShouldWriteCorrectDataType(string typeFullName, NanoCLRDataType dataType)
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

            TypeDefinition typeToTest = mscorlibAssemblyDefinition.MainModule.Types.First(i => i.FullName == typeFullName);

            nanoSignaturesTable table = new nanoSignaturesTable(context);
            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
            nanoBinaryWriter writer = nanoBinaryWriter.CreateBigEndianBinaryWriter(binaryWriter);

            // Act
            table.WriteDataTypeForTypeDef(typeToTest, writer);

            // Assert
            byte[] expectedBytes = new byte[] { (byte)dataType };
            byte[] actualBytes = memoryStream.ToArray();
            CollectionAssert.AreEqual(expectedBytes, actualBytes);
        }
    }
}
