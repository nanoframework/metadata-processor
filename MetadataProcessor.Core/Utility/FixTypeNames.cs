//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace nanoFramework.Tools.MetadataProcessor
{
    public class nanoHelpers
    {
        internal static string FixTypeNames(string name)
        {
            // This is used to remedy the wrong output from Cecil.Mono
            // Reported jbevain/cecil#715 
            // OK to remove if implemented

            // following II.23.2.16 Short form signatures
            string fixedName;

            fixedName = name.Replace("System.String", "string");
            fixedName = fixedName.Replace("System.Object", "object");
            fixedName = fixedName.Replace("System.Void", "void");
            fixedName = fixedName.Replace("System.Boolean", "bool");
            fixedName = fixedName.Replace("System.Char", "char");
            fixedName = fixedName.Replace("System.Byte", "int8");
            fixedName = fixedName.Replace("System.Sbyte", "uint8");
            fixedName = fixedName.Replace("System.Int16", "int16");
            fixedName = fixedName.Replace("System.UInt16", "uint16");
            fixedName = fixedName.Replace("System.Int32", "int32");
            fixedName = fixedName.Replace("System.UInt32", "uint32");
            fixedName = fixedName.Replace("System.Int64", "int64");
            fixedName = fixedName.Replace("System.UInt64", "uint64");
            fixedName = fixedName.Replace("System.Single", "float32");
            fixedName = fixedName.Replace("System.Double", "float64");

            return fixedName;
        }
    }
}
