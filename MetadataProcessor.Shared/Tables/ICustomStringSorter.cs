// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original work from Oleg Rakhmatulin.

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for sorting string literals before merging (solve strings order problem).
    /// </summary>
    public interface ICustomStringSorter
    {
        /// <summary>
        /// Sorts input sequence according needed logic.
        /// </summary>
        /// <param name="strings">Existing string listerals list.</param>
        /// <returns>Original string listerals list sorted according test pattern.</returns>
        IEnumerable<string> Sort(
            ICollection<string> strings);
    }
}
