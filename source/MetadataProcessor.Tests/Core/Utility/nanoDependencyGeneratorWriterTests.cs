using System;
using System.IO;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility
{
    [TestClass]
    public class nanoDependencyGeneratorWriterTests
    {
        [TestMethod]
        public void WriteTest()
        {
            var assemblyDefinition = TestObjectHelper.GetTestAssemblyDefinition();
            var nanoTablesContext = TestObjectHelper.GetInitializedNanoTablesContext();

            var iut = new nanoDependencyGeneratorWriter(assemblyDefinition, nanoTablesContext);


            using (var sw = new StringWriter())
            {
                using (var xw = XmlWriter.Create(sw))
                {
                    // test
                    iut.Write(xw);
                }
                
                var result = sw.ToString();

                //<?xml version="1.0" encoding="utf-16"?>
                //<AssemblyGraph>
                //    <Assembly Name="nanoFramework.Tools.MetadataProcessor.Tests" Version="1.0.0.0" Hash="0x00000000" Flags="0x00000000">
                //        <AssemblyRef Name="mscorlib" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="nanoFramework.Tools.MetadataProcessor.Core" Version="2.22.44.10207" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="Mono.Cecil" Version="0.11.2.0" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="Microsoft.VisualStudio.TestPlatform.TestFramework" Version="14.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="System.Drawing" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="System.Xml" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //        <AssemblyRef Name="System.Core" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //        <Type Name="&lt;Module&gt;" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.DummyCustomAttribute1" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.DummyCustomAttribute2" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.TestObjectHelper" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility.LoadHintsAssemblyResolverTests" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility.Crc32Tests" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility.nanoBitmapProcessorTests" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility.nanoDependencyGeneratorWriterTests" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables.nanoAttributesTableTests" Hash="0x00000000" />
                //        <Type Name="nanoFramework.Tools.MetadataProcessor.Tests.Core.Tables.nanoReferenceTableBaseTests" Hash="0x00000000" />
                //        <Type Name="&lt;PrivateImplementationDetails&gt;" Hash="0x00000000" />
                //    </Assembly>
                //    <Assembly Name="mscorlib" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="nanoFramework.Tools.MetadataProcessor.Core" Version="2.22.44.10207" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="Mono.Cecil" Version="0.11.2.0" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="Microsoft.VisualStudio.TestPlatform.TestFramework" Version="14.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="System.Drawing" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="System.Xml" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //    <Assembly Name="System.Core" Version="4.0.0.0" Hash="0x00000000" Flags="0x00000000" />
                //</AssemblyGraph>

                var xd = new XmlDocument();
                xd.LoadXml(result);

                // test for some points
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='nanoFramework.Tools.MetadataProcessor.Tests']/AssemblyRef[@Name='nanoFramework.Tools.MetadataProcessor.Core']"));
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='nanoFramework.Tools.MetadataProcessor.Tests']/Type[@Name='nanoFramework.Tools.MetadataProcessor.Tests.Core.Utility.nanoDependencyGeneratorWriterTests']"));
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='nanoFramework.Tools.MetadataProcessor.Core']"));


            }

        }
    }
}
