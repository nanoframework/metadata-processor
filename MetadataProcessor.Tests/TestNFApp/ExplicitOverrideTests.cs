// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestNFApp
{
    public interface IExplicitGeneric<T>
    {
        string Name();

        string Convert<U>(U value);
    }

    public interface IExplicitPlain
    {
        string Name();
    }

    public class ExplicitTwoInstances : IExplicitGeneric<int>, IExplicitGeneric<string>, IExplicitPlain
    {
        string IExplicitGeneric<int>.Name()
        {
            return "int";
        }

        string IExplicitGeneric<string>.Name()
        {
            return "string";
        }

        string IExplicitGeneric<int>.Convert<U>(U value)
        {
            return "int";
        }

        string IExplicitGeneric<string>.Convert<U>(U value)
        {
            return "string";
        }

        string IExplicitPlain.Name()
        {
            return "plain";
        }

        public string Other<U>(U value)
        {
            return "other";
        }
    }
}
