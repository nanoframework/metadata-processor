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
        // Key is the type itself
        internal static string GetTypeKey(TypeReference type)
        {
            var key = new System.Text.StringBuilder();

            AppendTypeKey(key, type);

            return key.ToString();
        }

        private static void AppendTypeKey(System.Text.StringBuilder key, TypeReference type)
        {
            switch (type)
            {
                case GenericParameter parameter:
                    key.Append(parameter.Type == GenericParameterType.Type ? "!" : "!!").Append(parameter.Position);
                    break;

                case GenericInstanceType instance:
                    AppendTypeKey(key, instance.ElementType);
                    key.Append('<');

                    foreach (TypeReference argument in instance.GenericArguments)
                    {
                        AppendTypeKey(key, argument);
                        key.Append(',');
                    }

                    key.Append('>');
                    break;

                case ArrayType array:
                    AppendTypeKey(key, array.ElementType);
                    key.Append('[').Append(array.Rank).Append(']');
                    break;

                case ByReferenceType byReference:
                    AppendTypeKey(key, byReference.ElementType);
                    key.Append('&');
                    break;

                case PointerType pointer:
                    AppendTypeKey(key, pointer.ElementType);
                    key.Append('*');
                    break;

                case IModifierType modifier:
                    AppendTypeKey(key, ((TypeSpecification)type).ElementType);
                    key.Append(modifier is RequiredModifierType ? " modreq(" : " modopt(");
                    AppendTypeKey(key, modifier.ModifierType);
                    key.Append(')');
                    break;

                default:
                    key.Append('[').Append(type.Scope?.Name).Append(']').Append(type.FullName);
                    break;
            }
        }

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
                return GetTypeKey(x) == GetTypeKey(y);
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
                // has to follow Equals
                return GetTypeKey(obj).GetHashCode();
            }
            else if (obj is GenericParameter genericParam)
            {
                // has to follow Equals: type (VAR or MVAR) and position, the owner doesn't count
                int hash = 17;
                hash = unchecked(hash * 31 + genericParam.Position);
                hash = unchecked(hash * 31 + (int)genericParam.Type);

                return hash;
            }
            else
            {
                // provide an hash code from the metadatatoken
                return obj.MetadataToken.GetHashCode();
            }
        }
    }
}
