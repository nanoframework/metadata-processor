//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using Mono.Cecil;
using System;
using System.Diagnostics;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encoded type for inline type calls.
    /// </summary>
    public class nanoMetadataToken
    {
        private ClrTable _clrTable;
        private ushort _id;

        public nanoMetadataToken()
        {
        }

        public nanoMetadataToken(ClrTable clrTable, ushort id)
        {
            _clrTable = clrTable;
            _id = id;
        }

        public nanoMetadataToken(MetadataToken token, ushort id)
        {
            _id = id;

            // get token type
            switch(token.TokenType)
            {
                case TokenType.AssemblyRef:
                    _clrTable = ClrTable.AssemblyRef;
                    break;

                case TokenType.TypeRef:
                    _clrTable = ClrTable.TypeRef;
                    break;

                case TokenType.Field:
                    _clrTable = ClrTable.FieldDef;
                    break;

                case TokenType.Method:
                    _clrTable = ClrTable.MethodRef;
                    break;

                case TokenType.TypeDef:
                    _clrTable = ClrTable.TypeDef;
                    break;

                //case TokenType.MethodSpec:
                //    _clrTable = ClrTable.FieldDef;
                //    break;

                //case TokenType.MethodSpec:
                //    _clrTable = ClrTable.MethodDef;
                //    break;

                case TokenType.GenericParam:
                    _clrTable = ClrTable.GenericParam;
                    break;

                case TokenType.MethodSpec:
                    _clrTable = ClrTable.MethodDef;
                    break;

                case TokenType.MemberRef:
                    _clrTable = ClrTable.MethodRef;
                    break;

                case TokenType.TypeSpec:
                    _clrTable = ClrTable.TypeSpec;
                    break;

                default:
                    Debug.Fail("Unsupported token conversion");
                    break;
            }
        }

        public override string ToString()
        {
            // table token
            var tokenType = (uint)_clrTable << 24;

            return $"{tokenType | _id:x8}";
        }
    }
}
