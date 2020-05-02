﻿using Mono.Cecil;
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

        public static Stream GetResourceStream(string resourceName)
        {
            if (String.IsNullOrWhiteSpace(resourceName))
            {
                throw new ArgumentNullException("resourceName");
            }

            Stream ret = null;

            var thisAssembly = Assembly.GetExecutingAssembly();

            ret = thisAssembly.GetManifestResourceStream(String.Concat(thisAssembly.GetName().Name, ".", resourceName));

            return ret;
        }

        public static byte[] GetResourceStreamContent(string resourceName)
        {
            byte[] ret = null;

            using (var resourceStream = GetResourceStream(resourceName))
            {
                if (resourceStream.Length > int.MaxValue)
                {
                    throw new NotImplementedException($"{resourceStream.Length} bytes");
                }

                using (var rdr = new BinaryReader(resourceStream))
                {
                    ret = rdr.ReadBytes((int)resourceStream.Length);
                }
            }

            return ret;
        }

    }
}
