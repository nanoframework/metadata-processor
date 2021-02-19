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
        private NanoCLRTable _clrTable;
        private ushort _id;

        public nanoMetadataToken()
        {
        }

        public nanoMetadataToken(NanoCLRTable clrTable, ushort id)
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
                    _clrTable = NanoCLRTable.TBL_AssemblyRef;
                    break;

                case TokenType.TypeRef:
                    _clrTable = NanoCLRTable.TBL_TypeRef;
                    break;

                case TokenType.Field:
                    _clrTable = NanoCLRTable.TBL_FieldDef;
                    break;

                case TokenType.Method:
                    _clrTable = NanoCLRTable.TBL_MethodRef;
                    break;

                case TokenType.TypeDef:
                    _clrTable = NanoCLRTable.TBL_TypeDef;
                    break;

                //case TokenType.MethodSpec:
                //    _clrTable = CLRTable.FieldDef;
                //    break;

                //case TokenType.MethodSpec:
                //    _clrTable = CLRTable.MethodDef;
                //    break;

                case TokenType.GenericParam:
                    _clrTable = NanoCLRTable.TBL_GenericParam;
                    break;

                case TokenType.MethodSpec:
                    _clrTable = NanoCLRTable.TBL_MethodDef;
                    break;

                case TokenType.MemberRef:
                    _clrTable = NanoCLRTable.TBL_MethodRef;
                    break;

                case TokenType.TypeSpec:
                    _clrTable = NanoCLRTable.TBL_TypeSpec;
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

            return $"{tokenType | _id:X8}";
        }
    }
}
