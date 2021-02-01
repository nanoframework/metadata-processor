//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using nanoFramework.Tools.MetadataProcessor.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace nanoFramework.Tools.MetadataProcessor
{
    internal sealed class nanoPdbxFileWriter
    {
        private readonly nanoTablesContext _context;

        public nanoPdbxFileWriter(
            nanoTablesContext context)
        {
            _context = context;
        }

        public void Write(
            XmlWriter writer)
        {
            writer.WriteStartElement("PdbxFile");
            writer.WriteStartElement("Assembly");

            WriteTokensPair(writer, _context.AssemblyDefinition.MetadataToken.ToUInt32(), 0x00000000);
            writer.WriteElementString("FileName", _context.AssemblyDefinition.MainModule.Name);
            WriteVersionInfo(writer, _context.AssemblyDefinition.Name.Version);

            writer.WriteStartElement("Classes");
            _context.TypeDefinitionTable.ForEachItems((token, item) => WriteClassInfo(writer, item, token));

            writer.WriteEndDocument();            
        }

        private void WriteVersionInfo(
            XmlWriter writer,
            Version version)
        {
            writer.WriteStartElement("Version");

            writer.WriteElementString("Major", version.Major.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteElementString("Minor", version.Minor.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteElementString("Build", version.Build.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteElementString("Revision", version.Revision.ToString("D", CultureInfo.InvariantCulture));

            writer.WriteEndElement();
        }

        #region Class output

        private void WriteClassInfo(
            XmlWriter writer,
            TypeDefinition item,
            uint nanoClrItemToken)
        {
            writer.WriteStartElement("Class");

            writer.WriteAttributeString("Name", item.FullName);
            writer.WriteAttributeString("IsEnum", item.IsEnum.ToString());
            writer.WriteAttributeString("NumGenericParams", item.GenericParameters.Count.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteAttributeString("IsGenericInstance", item.IsGenericInstance.ToString().ToLowerInvariant());

            WriteTokensPair(writer, item.MetadataToken.ToUInt32(), nanoClrTable.TBL_TypeDef.ToNanoTokenType() | nanoClrItemToken);

            writer.WriteStartElement("Methods");

            foreach (var tuple in GetMethodsTokens(item.Methods))
            {
                writer.WriteStartElement("Method");

                writer.WriteAttributeString("Name", tuple.Item3.Name);
                writer.WriteAttributeString("NumArgs", tuple.Item3.Parameters.Count.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("NumLocals", tuple.Item3.HasBody ? tuple.Item3.Body.Variables.Count.ToString("D", CultureInfo.InvariantCulture) : "0");
                writer.WriteAttributeString("NumGenericParams", tuple.Item3.GenericParameters.Count.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("IsGenericInstance", tuple.Item3.IsGenericInstance.ToString().ToLowerInvariant());

                WriteTokensPair(writer, tuple.Item1, tuple.Item2);

                if (!tuple.Item3.HasBody)
                {
                    writer.WriteElementString("HasByteCode", "false");
                }

                writer.WriteStartElement("ILMap");

                // sanity check vars
                uint prevItem1 = 0;
                uint prevItem2 = 0;

                foreach (var offset in _context.TypeDefinitionTable.GetByteCodeOffsets(tuple.Item1))
                {
                    if (prevItem1 > 0)
                    {
                        // 1st pass, load prevs with current values
                        Debug.Assert(prevItem1 < offset.Item1);
                        Debug.Assert(prevItem2 < offset.Item2);
                    }
                    writer.WriteStartElement("IL");

                    writer.WriteElementString("CLR", offset.Item1.ToString("X8", CultureInfo.InvariantCulture));
                    writer.WriteElementString("nanoCLR", offset.Item2.ToString("X8", CultureInfo.InvariantCulture));

                    prevItem1 = offset.Item1;
                    prevItem2 = offset.Item2;

                    // IL
                    writer.WriteEndElement();
                }

                // ILMap
                writer.WriteEndElement();

                // Method
                writer.WriteEndElement();
            }

            // Methods
            writer.WriteEndElement();

            writer.WriteStartElement("Fields");
            foreach (var tuple in GetFieldsTokens(item.Fields))
            {
                writer.WriteStartElement("Field");

                writer.WriteAttributeString("Name", tuple.Item3.Name);

                WriteTokensPair(writer, tuple.Item1, tuple.Item2);

                // Field
                writer.WriteEndElement();
            }
            // Fields
            writer.WriteEndElement();

            // Class
            writer.WriteEndElement();
        }

        private IEnumerable<Tuple<uint, uint, MethodDefinition>> GetMethodsTokens(
            IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                _context.MethodDefinitionTable.TryGetMethodReferenceId(method, out ushort methodToken);
                yield return new Tuple<uint, uint, MethodDefinition>(
                    method.MetadataToken.ToUInt32(), nanoClrTable.TBL_MethodDef.ToNanoTokenType() | methodToken, method);
            }
        }

        private IEnumerable<Tuple<uint, uint, FieldDefinition>> GetFieldsTokens(
            IEnumerable<FieldDefinition> fields)
        {
            foreach (var field in fields.Where(item => !item.HasConstant))
            {
                _context.FieldsTable.TryGetFieldReferenceId(field, false, out ushort fieldToken);
                yield return new Tuple<uint, uint, FieldDefinition>(
                    field.MetadataToken.ToUInt32(), nanoClrTable.TBL_FieldDef.ToNanoTokenType() | (uint)fieldToken, field);
            }
        }

        #endregion
        private void WriteTokensPair(
            XmlWriter writer,
            uint clrToken,
            uint nanoClrToken)
        {
            writer.WriteStartElement("Token");

            writer.WriteElementString("CLR", clrToken.ToString("X8", CultureInfo.InvariantCulture));
            writer.WriteElementString("nanoCLR", nanoClrToken.ToString("X8", CultureInfo.InvariantCulture));

            writer.WriteEndElement();
        }
    }
}
