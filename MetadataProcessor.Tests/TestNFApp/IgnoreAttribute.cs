// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace TestNFApp
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute
    {
        public IgnoreAttribute() : this(null)
        {
        }

        public IgnoreAttribute(string ignoreMessage)
        {
            IgnoreMessage = ignoreMessage;
        }

        private string _ignoreMessage;

        public string IgnoreMessage
        {
            get
            {
                return _ignoreMessage;
            }

            set
            {
                _ignoreMessage = value;
            }
        }
    }
}
