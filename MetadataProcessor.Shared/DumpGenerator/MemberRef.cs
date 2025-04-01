// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace nanoFramework.Tools.MetadataProcessor.Core
{
    public class MemberRef
    {
        public string ReferenceId;

        public string Name;

        public string Signature;

        public List<string> Arguments;
    }
}
