using System;
using System.Diagnostics.CodeAnalysis;

namespace TestNFApp
{
    public class ClassWithNullAttribs
    {
        [DoesNotReturn]
        public int MethodDecoratedWithDoesNotReturn()
        {
            throw new Exception();
        }

        public string MethoWIthParamNotNullAttrib([NotNull] string value)
        {
            return value;
        }
    }
}
