// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.Cecil;

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
        public readonly static List<NanoClrTable> NanoTypeTokenTables = new List<NanoClrTable>() {
                // <SYNC-WITH-NATIVE>
                // order matters and has to match CLR_UncompressTypeToken in native nanoCLR_Types.h
                NanoClrTable.TBL_TypeDef,
                NanoClrTable.TBL_TypeRef,
                NanoClrTable.TBL_TypeSpec,
                NanoClrTable.TBL_GenericParam
            };

        /// <summary>
        /// Tables to encode NanoTypeDefOrRefToken.
        /// </summary>
        public readonly static List<NanoClrTable> NanoTypeDefOrRefTokenTables = new List<NanoClrTable>() {
                NanoClrTable.TBL_TypeDef,
                NanoClrTable.TBL_TypeRef,
            };

        /// <summary>
        /// Tables to encode NanoMemberRefToken.
        /// </summary>
        public readonly static List<NanoClrTable> NanoMemberRefTokenTables = new List<NanoClrTable>() {
                // <SYNC-WITH-NATIVE>
                // order matters and has to match CLR_UncompressMethodToken in native nanoCLR_Types.h
                NanoClrTable.TBL_MethodDef,
                NanoClrTable.TBL_MethodRef,
                NanoClrTable.TBL_TypeSpec,
                NanoClrTable.TBL_MethodSpec,
            };

        /// <summary>
        /// Tables to encode NanoFieldMemberRefToken.
        /// </summary>
        public readonly static List<NanoClrTable> NanoFieldMemberRefTokenTables = new List<NanoClrTable>() {
                NanoClrTable.TBL_FieldDef,
                NanoClrTable.TBL_FieldRef
            };

        /// <summary>
        /// Tables to encode NanoMethodDefOrRefToken.
        /// </summary>
        public readonly static List<NanoClrTable> NanoMethodDefOrRefTokenTables = new List<NanoClrTable>() {
                NanoClrTable.TBL_MethodDef,
                NanoClrTable.TBL_MethodRef
            };

        /// <summary>
        /// Tables to encode NanoTypeOrMethodToken.
        /// </summary>
        public readonly static List<NanoClrTable> NanoTypeOrMethodDefTokenTables = new List<NanoClrTable>() {
                NanoClrTable.TBL_TypeDef,
                NanoClrTable.TBL_MethodDef
            };

        /// <summary>
        /// Tables to encode CLR_TypeRefOrSpec.
        /// </summary>
        public readonly static List<NanoClrTable> CLR_TypeRefOrSpecTables = new List<NanoClrTable>() {
                // <SYNC-WITH-NATIVE>
                // order matters and has to match decoder Owner() in native nanoCLR_Types.h
                NanoClrTable.TBL_TypeRef,
                NanoClrTable.TBL_TypeSpec
            };

        /// <summary>
        /// Encode table to be used in a nanoToken.
        /// The table index in moved to the MSbits.
        /// </summary>
        /// <param name="table">Table to compress.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The encoded tag to be used in a nanoToken.</returns>
        public static ushort EncodeTableIndex(NanoClrTable table, List<NanoClrTable> tableList)
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
        /// Decode <see cref="NanoClrTable"/> from nanoToken.
        /// </summary>
        /// <param name="value">Encoded value containing the table index.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The <see cref="NanoClrTable"/> encoded in the <paramref name="value"/>.</returns>
        public static NanoClrTable DecodeTableIndex(ushort value, List<NanoClrTable> tableList)
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
        public static ushort DecodeReferenceIndex(ushort value, List<NanoClrTable> tableList)
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
        /// Convert <see cref="TypeReference"/> (and derived) in <see cref="NanoClrTable"/>.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static NanoClrTable ConvertToNanoCLRTable(MemberReference value)
        {
            switch (value)
            {
                case GenericParameter _:
                    return NanoClrTable.TBL_GenericParam;

                case TypeDefinition _:
                    return NanoClrTable.TBL_TypeDef;

                case TypeSpecification _:
                    return NanoClrTable.TBL_TypeSpec;

                case TypeReference _:
                    return NanoClrTable.TBL_TypeRef;

                case FieldReference _:
                    return NanoClrTable.TBL_FieldRef;

                case MethodReference _:
                    return NanoClrTable.TBL_MethodRef;

                default:
                    throw new ArgumentException("Unknown conversion to CLR Table.");
            }
        }
    }
}
