//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encoded type for inline type calls.
    /// </summary>
    [Flags]
    public enum nanoEncodedInlineType : ushort
    {
        /// <summary>
        /// No encoding
        /// </summary>
        None = 0x0000,

        /// <summary>
        /// Type is GenericParameter
        /// </summary>
        GenericParam = 0x2000,

        /// <summary>
        /// Type is TypeReference.
        /// </summary>
        TypeRef = 0x4000,

        /// <summary>
        /// Type is TypeSpecification
        /// </summary>
        TypeSpec = 0x8000,

        /// <summary>
        /// Mask for all type flags
        /// </summary>
        EncodedInlineTypeMask =
            GenericParam |
            TypeRef |
            TypeSpec,
    }
}
