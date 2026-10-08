// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace StubsGenerationTestNFApp
{
    internal interface IFoo
    {
        int Bar { get; set; }
    }

    internal interface IGen<T>
    {
        T Item { get; set; }
    }

    internal interface IDict<TKey, TValue>
    {
        TValue Item { get; set; }
    }

    /// <summary>
    /// Explicit interface auto-properties: backing fields are named like
    /// '&lt;StubsGenerationTestNFApp.IFoo.Bar&gt;k__BackingField'.
    /// </summary>
    internal class ExplicitInterfaceProperties : IFoo, IGen<int>, IDict<string, int>
    {
        int IFoo.Bar { get; set; }

        int IGen<int>.Item { get; set; }

        int IDict<string, int>.Item { get; set; }

        // keyword-named auto-property
        public int @class { get; set; }
    }

    /// <summary>
    /// Primary constructor with a captured parameter: field named '&lt;width&gt;P'.
    /// </summary>
    internal class PrimaryConstructorHolder(int width)
    {
        public int Area(int height) => width * height;
    }

    internal delegate void VoidWork();

    internal delegate void IntWork(int value);

    /// <summary>
    /// Overloaded static method group conversions: delegates cached in compiler-generated '&lt;&gt;O' container
    /// as '&lt;0&gt;__SWork' and '&lt;1&gt;__SWork'.
    /// </summary>
    internal class MethodGroupConversions
    {
        private static void SWork()
        {
        }

        private static void SWork(int value)
        {
        }

        public void Run()
        {
            VoidWork first = SWork;
            IntWork second = SWork;

            first();
            second(1);
        }
    }
}
