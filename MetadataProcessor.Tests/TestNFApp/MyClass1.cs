// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using TestNFClassLibrary;

namespace TestNFApp
{
    // Define a class that has the custom attribute associated with one of its members.
    [Attribute2]
    [Attribute4]
    [Attribute1OnAnotherAssembly]
    public class MyClass1
    {
        [Attribute1]
        [Attribute3]
        [Attribute2OnAnotherAssembly]
        public void MyMethod1(int i)
        {
            return;
        }

        [Ignore("I'm ignoring you!")]
        public void MyMethodToIgnore()
        {

        }

        [DataRow((int)-1, (byte)2, (long)345678, (string)"A string", (bool)true)]
        [Complex(0xBEEF, "Another string", false)]
        public void MyMethodWithData()
        {
        }

        private readonly int _myField;

        [Ignore("I'm ignoring you!")]
        public int MyField => _myField;

        [Max(0xDEADBEEF)]
        [Author("William Shakespeare")]
        public int MyPackedField;
    }
}
