//
// Copyright (c) .NET Foundation and Contributors
// Original work from Oleg Rakhmatulin.
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
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

        private void WriteClassInfo(
            XmlWriter writer,
            TypeDefinition item,
            uint nanoClrItemToken)
        {
            writer.WriteStartElement("Class");

            writer.WriteElementString("Name", item.FullName);

            writer.WriteElementString("IsValueType", item.IsValueType.ToString());
            writer.WriteElementString("IsEnum", item.IsEnum.ToString());
            writer.WriteElementString("NumGenericParams", item.GenericParameters.Count.ToString("D", CultureInfo.InvariantCulture));
            writer.WriteElementString("IsGenericInstance", item.IsGenericInstance.ToString().ToLowerInvariant());

            WriteTokensPair(writer, item.MetadataToken.ToUInt32(), 0x04000000 | nanoClrItemToken);

            writer.WriteStartElement("Methods");
            foreach (var tuple in GetMethodsTokens(item.Methods))
            {
                writer.WriteStartElement("Method");

                WriteTokensPair(writer, tuple.Item1, tuple.Item2);

                writer.WriteElementString("Name", tuple.Item3.Name);
                writer.WriteElementString("NumArgs", tuple.Item3.Parameters.Count.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteElementString("NumLocals", tuple.Item3.HasBody ? tuple.Item3.Body.Variables.Count.ToString("D", CultureInfo.InvariantCulture) : "0");
                writer.WriteElementString("MaxStack", CodeWriter.CalculateStackSize(tuple.Item3.Body).ToString("D", CultureInfo.InvariantCulture));
                writer.WriteElementString("NumGenericParams", tuple.Item3.GenericParameters.Count.ToString("D", CultureInfo.InvariantCulture));
                writer.WriteElementString("IsGenericInstance", tuple.Item3.IsGenericInstance.ToString().ToLowerInvariant());

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

                    writer.WriteElementString("CLR", "0x" + offset.Item1.ToString("X8", CultureInfo.InvariantCulture));
                    writer.WriteElementString("nanoCLR", "0x" + offset.Item2.ToString("X8", CultureInfo.InvariantCulture));

                    prevItem1 = offset.Item1;
                    prevItem2 = offset.Item2;

                    writer.WriteEndElement();
                }
                writer.WriteEndElement();

                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteStartElement("Fields");
            foreach (var tuple in GetFieldsTokens(item.Fields))
            {
                writer.WriteStartElement("Field");

                writer.WriteElementString("Name", tuple.Item3.Name);

                WriteTokensPair(writer, tuple.Item1, tuple.Item2);

                writer.WriteEndElement();
            }
            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        private IEnumerable<Tuple<uint, uint, MethodDefinition>> GetMethodsTokens(
            IEnumerable<MethodDefinition> methods)
        {
            foreach (var method in methods)
            {
                _context.MethodDefinitionTable.TryGetMethodReferenceId(method, out ushort methodToken);
                yield return new Tuple<uint, uint, MethodDefinition>(
                    method.MetadataToken.ToUInt32(), 0x06000000 | (uint)methodToken, method);
            }
        }

        private IEnumerable<Tuple<uint, uint, FieldDefinition>> GetFieldsTokens(
            IEnumerable<FieldDefinition> fields)
        {
            foreach (var field in fields.Where(item => !item.HasConstant))
            {
                _context.FieldsTable.TryGetFieldReferenceId(field, false, out ushort fieldToken);
                yield return new Tuple<uint, uint, FieldDefinition>(
                    field.MetadataToken.ToUInt32(), 0x05000000 | (uint)fieldToken, field);
            }
        }

        private void WriteTokensPair(
            XmlWriter writer,
            uint clrToken,
            uint nanoClrToken)
        {
            writer.WriteStartElement("Token");

            writer.WriteElementString("CLR", "0x" + clrToken.ToString("X8", CultureInfo.InvariantCulture));
            writer.WriteElementString("nanoCLR", "0x" + nanoClrToken.ToString("X8", CultureInfo.InvariantCulture));

            writer.WriteEndElement();
        }
    }
}
