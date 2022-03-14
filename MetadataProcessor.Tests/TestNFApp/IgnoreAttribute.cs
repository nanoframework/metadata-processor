﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

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
