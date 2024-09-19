//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace TestNFApp
{
    public class TestingDestructors
    {
        public TestingDestructors()
        {
            Console.WriteLine("Test Destructors class 3");

            if (DestructorsTestClass.TestMethod())
            {
                Console.WriteLine("Test Destructors class 3 passed!");
            }
            else
            {
                Console.WriteLine("Test Destructors class 3 failed!");
            }

            Console.WriteLine("Test Destructors class 4");
            if (DestructorsTestAnotherClass.TestMethod())
            {
                Console.WriteLine("Test Destructors class 4 passed!");
            }
            else
            {
                Console.WriteLine("Test Destructors class 4 failed!");
            }

            Console.WriteLine("Test destructors completed!");
        }
    }

    public class DestructorsTestClass
    {
        static int intI = 1;

        ~DestructorsTestClass()
        {
            // Calling Destructor for Test Class 3
            intI = 2;
     
            Console.WriteLine("Calling Destructor for Test Class 3");
        }

        public static bool TestMethod()
        {
            DestructorsTestClass mc = new DestructorsTestClass();
            mc = null;

            // should be calling GC
            // nanoFramework.Runtime.Native.GC.Run(true);

            int sleepTime = 5000;
            int slept = 0;

            while (intI != 2 && slept < sleepTime)
            {
                System.Threading.Thread.Sleep(10);
                slept += 10;
            }

            // Thread has slept for
            Console.WriteLine($"Thread has slept for {slept}");

            if (intI == 2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    public class DestructorsTestAnotherClassBase
    {
        public static int intI = 2;

        ~DestructorsTestAnotherClassBase()
        {
            intI = intI * 2;

            Console.WriteLine("Calling Destructor for Test Class 4 Base");
        }
    }

    public class DestructorsTestAnotherClass : DestructorsTestAnotherClassBase
    {
        ~DestructorsTestAnotherClass()
        {
            intI = intI + 2;

            Console.WriteLine("Calling Destructor for Test Class 4");
        }

        public static bool TestMethod()
        {
            DestructorsTestAnotherClass mc = new DestructorsTestAnotherClass();
            
            mc = null;

            // should be calling GC
            // nanoFramework.Runtime.Native.GC.Run(true);

            int sleepTime = 5000;
            int slept = 0;
            
            while (intI != 8 && slept < sleepTime)
            {
                System.Threading.Thread.Sleep(10);
                slept += 10;
            }
            
            Console.WriteLine($"Thread has slept for {slept}");
            
            if (intI == 8)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
