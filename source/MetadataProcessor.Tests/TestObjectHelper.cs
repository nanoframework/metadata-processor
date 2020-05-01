using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace nanoFramework.Tools.MetadataProcessor.Tests
{
    public static class TestObjectHelper
    {
        public static nanoTablesContext GetInitializedNanoTablesContext()
        {
            nanoTablesContext ret = null;

            var assemblyDefinition = GetTestAssemblyDefinition();

            ret = new nanoTablesContext(
                assemblyDefinition, 
                null,
                new List<string>(),
                null,
                false,
                false,
                false);

            return ret;
        }

        public static AssemblyDefinition GetTestAssemblyDefinition()
        {
            AssemblyDefinition ret = AssemblyDefinition.ReadAssembly(Assembly.GetExecutingAssembly().Location);
            return ret;
        }
    }
}
