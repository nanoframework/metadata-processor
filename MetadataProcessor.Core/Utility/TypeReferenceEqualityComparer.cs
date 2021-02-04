//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for comparing two instances of <see cref="TypeReference"/> objects
    /// using <see cref="TypeReference.FullName"/> property as unique key for comparison.
    /// </summary>
    public sealed class TypeReferenceEqualityComparer : IEqualityComparer<TypeReference>
    {
        /// <inheritdoc/>
        public bool Equals(TypeReference x, TypeReference y)
        {
            if (x is null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y is null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            return string.Equals(x.FullName, y.FullName, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public int GetHashCode(TypeReference obj)
        {
            if (obj is null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return obj.FullName.GetHashCode();
        }
    }
}
