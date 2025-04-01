// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class MethodDef
    {
        public string ReferenceId;

        public string Flags;

        public string Implementation;

        public string RVA;

        public string Name;

        public string Signature;

        public string Locals;

        public List<ExceptionHandler> ExceptionHandlers = new List<ExceptionHandler>();

        public string ILCodeInstructionsCount;

        public List<ILCode> ILCode = new List<ILCode>();
    }
}
