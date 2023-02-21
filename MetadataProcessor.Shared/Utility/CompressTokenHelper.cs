//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helpers to handle nanoTokens.
    /// </summary>
    public class nanoTokenHelpers
    {
        /// <summary>
        /// Tables to encode NanoTypeToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoTypeTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_TypeDef,
                NanoCLRTable.TBL_TypeRef,
                NanoCLRTable.TBL_TypeSpec,
                NanoCLRTable.TBL_GenericParam
            };

        /// <summary>
        /// Tables to encode NanoTypeDefOrRefToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoTypeDefOrRefTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_TypeDef,
                NanoCLRTable.TBL_TypeRef,
            };

        /// <summary>
        /// Tables to encode NanoMemberRefToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoMemberRefTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_MethodDef,
                NanoCLRTable.TBL_MethodRef,
                NanoCLRTable.TBL_TypeSpec,
                NanoCLRTable.TBL_MethodSpec,
            };

        /// <summary>
        /// Tables to encode NanoFieldMemberRefToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoFieldMemberRefTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_FieldDef,
                NanoCLRTable.TBL_FieldRef
            };

        /// <summary>
        /// Tables to encode NanoMethodDefOrRefToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoMethodDefOrRefTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_MethodDef,
                NanoCLRTable.TBL_MethodRef
            };

        /// <summary>
        /// Tables to encode NanoTypeOrMethodToken.
        /// </summary>
        public readonly static List<NanoCLRTable> NanoTypeOrMethodDefTokenTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_TypeDef,
                NanoCLRTable.TBL_MethodDef
            };

        /// <summary>
        /// Tables to encode CLR_TypeRefOrSpec.
        /// </summary>
        public readonly static List<NanoCLRTable> CLR_TypeRefOrSpecTables = new List<NanoCLRTable>() {
                NanoCLRTable.TBL_TypeRef,
                NanoCLRTable.TBL_TypeSpec
            };

        /// <summary>
        /// Encode table to be used in a nanoToken.
        /// The table index in moved to the MSbits.
        /// </summary>
        /// <param name="table">Table to compress.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The encoded tag to be used in a nanoToken.</returns>
        public static ushort EncodeTableIndex(NanoCLRTable table, List<NanoCLRTable> tableList)
        {
            // sanity checks
            if (tableList.Count < 1)
            {
                Debug.Fail($"List contains only one element. No need to encode.");
            }

            if (!tableList.Contains(table))
            {
                Debug.Fail($"{table} is not listed in the options.");
            }

            // find out how many bits are required to compress the list
            var requiredBits = (int)Math.Round(Math.Log(tableList.Count, 2));

            return (ushort)(tableList.IndexOf(table) << (16 - requiredBits));
        }

        /// <summary>
        /// Decode <see cref="NanoCLRTable"/> from nanoToken.
        /// </summary>
        /// <param name="value">Encoded value containing the table index.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The <see cref="NanoCLRTable"/> encoded in the <paramref name="value"/>.</returns>
        public static NanoCLRTable DecodeTableIndex(ushort value, List<NanoCLRTable> tableList)
        {
            if (tableList.Count < 1)
            {
                Debug.Fail($"List contains only one element. No need to encode.");
            }

            // find out how many bits are required to compress the list
            var requiredBits = (int)Math.Round(Math.Log(tableList.Count, 2));

            var index = (value >> 16 - requiredBits);

            return tableList[index];
        }

        /// <summary>
        /// Decode the reference from nanoToken taking into account the encoded table.
        /// </summary>
        /// <param name="value">Encoded value.</param>
        /// <param name="tableList">List of tables used in encoding.</param>
        /// <returns>The reference encoded in the <paramref name="value"/>.</returns>
        public static ushort DecodeReferenceIndex(ushort value, List<NanoCLRTable> tableList)
        {
            if (tableList.Count < 1)
            {
                Debug.Fail($"List contains only one element. No need to encode.");
            }

            // find out how many bits are required to compress the list
            var requiredBits = (int)Math.Log(tableList.Count, 2);

            var mask = 0xFFFF;

            while (requiredBits-- > 0)
            {
                mask = mask >> 1;
            }

            return (ushort)(value & mask);
        }

        /// <summary>
        /// Convert <see cref="TypeReference"/> (and derived) in <see cref="NanoCLRTable"/>.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static NanoCLRTable ConvertToNanoCLRTable(MemberReference value)
        {
            switch (value)
            {
                case GenericParameter _:
                    return NanoCLRTable.TBL_GenericParam;

                case TypeDefinition _:
                    return NanoCLRTable.TBL_TypeDef;

                case TypeSpecification _:
                    return NanoCLRTable.TBL_TypeSpec;

                case TypeReference _:
                    return NanoCLRTable.TBL_TypeRef;

                case FieldReference _:
                    return NanoCLRTable.TBL_FieldRef;

                case MethodReference _:
                    return NanoCLRTable.TBL_MethodRef;

                default:
                    throw new ArgumentException("Unknown conversion to CLR Table.");
            }
        }
    }
}
