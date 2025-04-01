// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Helper class for comparing two instances of <see cref="TypeReference"/> objects
    /// using <see cref="TypeReference.FullName"/> property as unique key for comparison.
    /// </summary>
    public sealed class TypeReferenceEqualityComparer : IEqualityComparer<TypeReference>
    {
        private readonly nanoTablesContext _context;

        public TypeReferenceEqualityComparer(nanoTablesContext context) => _context = context;

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

            if (x is TypeSpecification &&
                !(y is TypeSpecification))
            {
                return false;
            }
            else if (y is TypeSpecification &&
                !(x is TypeSpecification))
            {
                return false;
            }
            else if (x is TypeSpecification &&
                     y is TypeSpecification)
            {
                // get signatures to perform comparison
                ushort xSignatureId = _context.SignaturesTable.GetOrCreateSignatureId(x);
                ushort ySignatureId = _context.SignaturesTable.GetOrCreateSignatureId(y);

                return xSignatureId == ySignatureId;
            }
            else if (x is GenericParameter && y is GenericParameter)
            {
                // comparison is made with type and position
                var xGenericParam = x as GenericParameter;
                var yGenericParam = y as GenericParameter;

                return (xGenericParam.Type == yGenericParam.Type) &&
                    (xGenericParam.Position == yGenericParam.Position);
            }
            else
            {
                return x.MetadataToken == y.MetadataToken;
            }
        }

        /// <inheritdoc/>
        public int GetHashCode(TypeReference obj)
        {
            if (obj is null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (obj is TypeSpecification)
            {
                ushort xSignatureId = _context.SignaturesTable.GetOrCreateSignatureId(obj);

                // provide an hash code based on the TypeSpec signature 
                return xSignatureId;
            }
            else if (obj is GenericParameter)
            {
                // provide an hash code based on the generic parameter position and type, 
                // which is what makes it unique when comparing GenericParameter as a TypeReference
                var genericParam = obj as GenericParameter;

                return genericParam.Position * 10 + (int)genericParam.Type;
            }
            else
            {
                // provide an hash code from the metadatatoken
                return obj.MetadataToken.GetHashCode();
            }
        }
    }
}
