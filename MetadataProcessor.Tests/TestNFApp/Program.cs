// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using TestNFClassLibrary;

namespace TestNFApp
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine("Starting TestNFApp");

            ///////////////////////////////////////////////////////////////////
            // referenced class
            Console.WriteLine("++++++++++++++++++++++++++++");
            Console.WriteLine("++ Referenced class tests ++");
            Console.WriteLine("++++++++++++++++++++++++++++");
            Console.WriteLine("");

            // instantiating a class on another assembly
            ClassOnAnotherAssembly anotherClass = new ClassOnAnotherAssembly();

            //////////////////////////////
            // accessing property on class
            int dummyMirror1 = anotherClass.DummyProperty;

            Console.WriteLine($"Accessed property on class: {dummyMirror1} ");

            /////////////////////////////////////////////////////////////////////////
            // instantiating a class on another assembly with a constructor parameter
            Console.WriteLine("Instantiating a class on another assembly with a constructor parameter");
            anotherClass = new ClassOnAnotherAssembly(99);

            //////////////////////////////
            // accessing property on class
            _ = anotherClass.DummyProperty;

            Console.WriteLine($"Accessed property on class: {dummyMirror1}");

            /////////////////////////////
            // Reflection Tests
            Console.WriteLine("Reflection Tests");
            ReflectionTests();

            ///////////////////////////////////////
            // Delegate and MulticastDelegate tests
            Console.WriteLine("Delegate and MulticastDelegate tests");
            _ = new TestingDelegates();

            ////////////////////////////////////////////////
            // Test enum in another assembly, same namespace
            Console.WriteLine("Test enum in another assembly, same namespace");
            TestEnumInAnotherAssembly enumTest = new TestEnumInAnotherAssembly();
            enumTest.CallTestEnumInAnotherAssembly();

            /////////////////////////////////////
            // reference enum in another assembly
            IAmAClassWithAnEnum.EnumA enumA = (IAmAClassWithAnEnum.EnumA)1;

            Console.WriteLine($"Reference enum in another assembly {enumA}");

            //////////////////////////////////////////
            Console.WriteLine("Test accessing enum in another assembly");
            IAmAClassWithAnEnum.EnumA messageType = (IAmAClassWithAnEnum.EnumA)0;
            switch (messageType)
            {
                case IAmAClassWithAnEnum.EnumA.Test:
                    Console.WriteLine($"Not good... {messageType}");
                    break;

                default:
                    Console.WriteLine("all good");
                    break;
            }

            ///////////////////////////////////////////////////////////////////
            // Generics Tests
            _ = new GenericClassTests();
            _ = new StatckTests();

            // null attributes tests
            Console.WriteLine("Null attributes tests");
            _ = new ClassWithNullAttribs();

            Console.WriteLine("Exiting TestNFApp");
        }

        private static void MiscelaneousTests()
        {
            var type = typeof(short[]);
            if (type.FullName != "System.Int16[]")
            {
                throw new Exception($"Type name is wrong. Got '{type.FullName}' should be System.Int16[]");
            }
        }

        public static void ReflectionTests()
        {
            Console.WriteLine("++++++++++++++++++++++");
            Console.WriteLine("++ Reflection tests ++");
            Console.WriteLine("++++++++++++++++++++++");
            Console.WriteLine("");

            // Get the type of MyClass1.
            Type myType = typeof(MyClass1);

            // Display the attributes of MyClass1.
            object[] myAttributes = myType.GetCustomAttributes(true);

            Console.WriteLine("");
            Console.WriteLine($"'{myType.Name}' type has {myAttributes.Length} custom attributes");

            if (myAttributes.Length > 0)
            {
                Console.WriteLine("");
                Console.WriteLine($"The attributes for the class '{myType.Name}' are:");

                for (int j = 0; j < myAttributes.Length; j++)
                {
                    Console.WriteLine($"  {myAttributes[j]}");
                }
            }

            // Get the methods associated with MyClass1.
            MemberInfo[] myMethods = myType.GetMethods();

            Console.WriteLine("");
            Console.WriteLine($"'{myType.Name}' type has '{myMethods.Length}' methods");

            // Display the attributes for each of the methods of MyClass1.
            for (int i = 0; i < myMethods.Length; i++)
            {
                string methodName = myMethods[i].Name;

                Console.WriteLine("");
                Console.WriteLine($"Getting custom attributes for '{methodName}'");

                myAttributes = myMethods[i].GetCustomAttributes(true);

                if (myAttributes.Length > 0)
                {
                    Console.WriteLine("");
                    Console.WriteLine($"'{methodName}' method has {myAttributes.Length} custom attributes");

                    Console.WriteLine("");
                    Console.WriteLine($"The attributes for the method '{methodName}' of class '{myType.Name}' are:");

                    for (int j = 0; j < myAttributes.Length; j++)
                    {
                        Console.WriteLine("");
                        Console.WriteLine($"  {myAttributes[j]}");

                        // check if the method has IgnoreAttribute
                        if (myAttributes[j] is IgnoreAttribute)
                        {
                            Console.WriteLine($"  >>>>>>> {methodName} has 'IgnoreAttribute' attribute");
                        }

                        // check if the method has DataRowAttribute
                        if (myAttributes[j] is DataRowAttribute)
                        {
                            Console.WriteLine($"  >>>>>>> {methodName} has 'DataRowAttribute' attribute");

                            DataRowAttribute attDataRow = (DataRowAttribute)myAttributes[j];

                            int index = 0;

                            foreach (object dataRow in attDataRow.Arguments)
                            {
                                Console.WriteLine($"          DataRowAttribute.Arg[{index++}] has: {dataRow}");
                            }
                        }

                        // check if the method has ComplexAttribute
                        if (myAttributes[j] is ComplexAttribute)
                        {
                            Console.WriteLine($"  >>>>>>> {methodName} has 'ComplexAttribute' attribute");

                            ComplexAttribute attDataRow = (ComplexAttribute)myAttributes[j];

                            Console.WriteLine($"          ComplexAttribute.Max is {attDataRow.Max}");
                            Console.WriteLine($"          ComplexAttribute.B   is {attDataRow.B}");
                            Console.WriteLine($"          ComplexAttribute.S   is {attDataRow.S}");
                        }

                    }
                }
            }

            // display the custom attributes with constructor
            MyClass1 myClass = new MyClass1();

            object[] myFieldAttributes = myClass.GetType().GetField("MyPackedField").GetCustomAttributes(true);

            Console.WriteLine("");
            Console.WriteLine($"The custom attributes of field 'MyPackedField' are:");

            MaxAttribute attMax = (MaxAttribute)myFieldAttributes[0];
            Console.WriteLine("");
            Console.WriteLine($"MaxAttribute value is: 0x{attMax.Max.ToString("X8")}");

            AuthorAttribute attAuthor = (AuthorAttribute)myFieldAttributes[1];
            Console.WriteLine("");
            Console.WriteLine($"AuthorAttribute value is: '{attAuthor.Author}'");

            Console.WriteLine("");
            Console.WriteLine("+++ReflectionTests completed");
            Console.WriteLine("");
        }
    }
}
