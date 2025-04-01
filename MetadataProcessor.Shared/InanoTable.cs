// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Common interface for all metadata tables in .NET nanoFramework assembly.
    /// </summary>
    public interface InanoTable
    {
        /// <summary>
        /// Writes metadata table from memory representation into output stream.
        /// </summary>
        /// <param name="writer">Binary writer with correct endianness.</param>
        void Write(
            nanoBinaryWriter writer);
    }
}
