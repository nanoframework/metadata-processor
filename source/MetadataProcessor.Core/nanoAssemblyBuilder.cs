//
// Copyright (c) 2019 The nanoFramework project contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Main metadata transformation class - builds .NET nanoFramework assembly
    /// from full .NET Framework assembly metadata represented in Mono.Cecil format.
    /// </summary>
    public sealed class nanoAssemblyBuilder
    {
        private readonly nanoTablesContext _tablesContext;

        private readonly bool _minimize;
        private readonly bool _verbose;

        public nanoTablesContext TablesContext => _tablesContext;

        /// <summary>
        /// Creates new instance of <see cref="nanoAssemblyBuilder"/> object.
        /// </summary>
        /// <param name="assemblyDefinition">Original assembly metadata in Mono.Cecil format.</param>
        /// <param name="explicitTypesOrder">List of full type names with explicit ordering.</param>
        /// <param name="stringSorter">Custom string literals sorter for UTs using only.</param>
        /// <param name="applyAttributesCompression">
        /// If contains <c>true</c> each type/method/field should contains one attribute of each type.
        /// </param>
        public nanoAssemblyBuilder(
            AssemblyDefinition assemblyDefinition,
            List<string> classNamesToExclude,
            bool minimize,
            bool verbose,
            List<string> explicitTypesOrder = null,
            ICustomStringSorter stringSorter = null,
            bool applyAttributesCompression = false)
        {
            _tablesContext = new nanoTablesContext(
                assemblyDefinition, 
                explicitTypesOrder,
                classNamesToExclude,
                stringSorter,
                applyAttributesCompression);

            _minimize = minimize;
            _verbose = verbose;
        }

        /// <summary>
        /// Writes all .NET nanoFramework metadata into output stream.
        /// </summary>
        /// <param name="binaryWriter">Binary writer with correct endianness.</param>
        public void Write(
            nanoBinaryWriter binaryWriter)
        {
            var header = new nanoAssemblyDefinition(_tablesContext);
            header.Write(binaryWriter, true);

            foreach (var table in GetTables(_tablesContext))
            {
                var tableBegin = (binaryWriter.BaseStream.Position + 3) & 0xFFFFFFFC;
                table.Write(binaryWriter);

                var padding = (4 - ((binaryWriter.BaseStream.Position - tableBegin) % 4)) % 4;
                binaryWriter.WriteBytes(new byte[padding]);

                header.UpdateTableOffset(binaryWriter, tableBegin, padding);
            }

            binaryWriter.BaseStream.Seek(0, SeekOrigin.Begin);
            header.Write(binaryWriter, false);
        }

        public void Write(
            XmlWriter xmlWriter)
        {
            var pdbxWriter = new nanoPdbxFileWriter(_tablesContext);
            pdbxWriter.Write(xmlWriter);
        }

        private static IEnumerable<InanoTable> GetTables(
            nanoTablesContext context)
        {
            yield return context.AssemblyReferenceTable;

            yield return context.TypeReferencesTable;

            yield return context.FieldReferencesTable;

            yield return context.MethodReferencesTable;

            yield return context.TypeDefinitionTable;

            yield return context.FieldsTable;

            yield return context.MethodDefinitionTable;

            yield return context.AttributesTable;

            yield return context.TypeSpecificationsTable;

            yield return context.ResourcesTable;

            yield return context.ResourceDataTable;

            context.ByteCodeTable.UpdateStringTable();
            context.StringTable.GetOrCreateStringId(
                context.AssemblyDefinition.Name.Name);

            yield return context.StringTable;
            
            yield return context.SignaturesTable;

            yield return context.ByteCodeTable;

            yield return context.ResourceFileTable;

            yield return nanoEmptyTable.Instance;
        }
    }
}
