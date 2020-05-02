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
            var assemblyDefinition = TestObjectHelper.GetTestNFAppAssemblyDefinition();
            var nanoTablesContext = TestObjectHelper.GetTestNFAppNanoTablesContext();

            var iut = new nanoDependencyGeneratorWriter(assemblyDefinition, nanoTablesContext);


            using (var sw = new StringWriter())
            {
                using (var xw = XmlWriter.Create(sw))
                {
                    // test
                    iut.Write(xw);
                }
                
                var result = sw.ToString();

                // the minimum:
                // <?xml version="1.0" encoding="utf-16"?>
                // <AssemblyGraph>
                //  <Assembly Name="TestNFApp" Version="1.0.0.0" Hash="0x00000000" Flags="0x00000000">
                //      <AssemblyRef Name="mscorlib" Version="1.3.0.3" Hash="0x00000000" Flags="0x00000000" />
                //      <Type Name="&lt;Module&gt;" Hash="0x00000000" />
                //      <Type Name="TestNFApp.Program" Hash="0x00000000" />
                //  </Assembly>
                //  <Assembly Name="mscorlib" Version="1.3.0.3" Hash="0x00000000" Flags="0x00000000" />
                // </AssemblyGraph>

                var xd = new XmlDocument();
                xd.LoadXml(result);

                // test for some points
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='TestNFApp']/AssemblyRef[@Name='mscorlib']"));
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='TestNFApp']/Type[@Name='TestNFApp.Program']"));
                Assert.IsNotNull(xd.SelectSingleNode("//AssemblyGraph/Assembly[@Name='mscorlib']"));


            }

        }
    }
}
