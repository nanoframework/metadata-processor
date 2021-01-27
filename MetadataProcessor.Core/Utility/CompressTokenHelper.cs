//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

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
        public readonly static List<ClrTable> NanoTypeTokenTables = new List<ClrTable>() {
                ClrTable.TypeDef,
                ClrTable.TypeRef,
                ClrTable.TypeSpec,
                ClrTable.GenericParam
            };

        /// <summary>
        /// Tables to encode NanoMethodToken.
        /// </summary>
        public readonly static List<ClrTable> NanoMethodTokenTables = new List<ClrTable>() {
                ClrTable.MethodDef,
                ClrTable.MethodRef
            };

        /// <summary>
        /// Encode table to be used in a nanoToken.
        /// The table index in moved to the MSbits.
        /// </summary>
        /// <param name="table">Table to compress.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The encoded tag to be used in a nanoToken.</returns>
        public static ushort EncodeTableIndex(ClrTable table, List<ClrTable> tableList)
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
            var requiredBits = (int)Math.Log(tableList.Count, 2);

            return (ushort)(tableList.IndexOf(table) << (16 - requiredBits));
        }

        /// <summary>
        /// Decode <see cref="ClrTable"/> from nanoToken.
        /// </summary>
        /// <param name="value">Encoded value containing the table index.</param>
        /// <param name="tableList">List of tables to be used in encoding.</param>
        /// <returns>The <see cref="ClrTable"/> encoded in the <paramref name="value"/>.</returns>
        public static ClrTable DecodeTableIndex(ushort value, List<ClrTable> tableList)
        {
            if (tableList.Count < 1)
            {
                Debug.Fail($"List contains only one element. No need to encode.");
            }

            // find out how many bits are required to compress the list
            var requiredBits = (int)Math.Log(tableList.Count, 2);

            var index = (value >> 16 - requiredBits);

            return tableList[index];
        }

        /// <summary>
        /// Decode the reference from nanoToken taking into account the encoded table.
        /// </summary>
        /// <param name="value">Encoded value.</param>
        /// <param name="tableList">List of tables used in encoding.</param>
        /// <returns>The reference encoded in the <paramref name="value"/>.</returns>
        public static ushort DecodeReferenceIndex(ushort value, List<ClrTable> tableList)
        {
            if (tableList.Count < 1)
            {
                Debug.Fail($"List contains only one element. No need to encode.");
            }

            // find out how many bits are required to compress the list
            var requiredBits = (int)Math.Log(tableList.Count, 2);

            var mask = 0xFFFF;

            while(requiredBits-- > 0)
            {
                mask = mask >> 1;
            }

            return (ushort)(value & mask);
        }
    }
}
