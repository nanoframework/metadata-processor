// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Mono.Cecil;

namespace nanoFramework.Tools.MetadataProcessor
{
    /// <summary>
    /// Encoded type for inline type calls.
    /// </summary>
    public class nanoMetadataToken
    {
        private NanoClrTable _clrTable;
        private ushort _id;

        public nanoMetadataToken()
        {
        }

        public nanoMetadataToken(NanoClrTable clrTable, ushort id)
        {
            _clrTable = clrTable;
            _id = id;
        }

        public nanoMetadataToken(MetadataToken token, ushort id)
        {
            _id = id;

            // get token type
            switch (token.TokenType)
            {
                case TokenType.AssemblyRef:
                    _clrTable = NanoClrTable.TBL_AssemblyRef;
                    break;

                case TokenType.TypeRef:
                    _clrTable = NanoClrTable.TBL_TypeRef;
                    break;

                case TokenType.Field:
                    _clrTable = NanoClrTable.TBL_FieldDef;
                    break;

                case TokenType.Method:
                    _clrTable = NanoClrTable.TBL_MethodDef;
                    break;

                case TokenType.TypeDef:
                    _clrTable = NanoClrTable.TBL_TypeDef;
                    break;

                //case TokenType.MethodSpec:
                //    _clrTable = CLRTable.FieldDef;
                //    break;

                //case TokenType.MethodSpec:
                //    _clrTable = CLRTable.MethodDef;
                //    break;

                case TokenType.GenericParam:
                    _clrTable = NanoClrTable.TBL_GenericParam;
                    break;

                case TokenType.MethodSpec:
                    _clrTable = NanoClrTable.TBL_MethodDef;
                    break;

                case TokenType.MemberRef:
                    _clrTable = NanoClrTable.TBL_MethodRef;
                    break;

                case TokenType.TypeSpec:
                    _clrTable = NanoClrTable.TBL_TypeSpec;
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
