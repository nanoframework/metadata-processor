﻿//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.Reflection;
using TestNFClassLibrary;

namespace TestNFApp
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Starting TestNFApp");

            ///////////////////////////////////////////////////////////////////
            // referenced class
            Debug.WriteLine("++++++++++++++++++++++++++++");
            Debug.WriteLine("++ Referenced class tests ++");
            Debug.WriteLine("++++++++++++++++++++++++++++");
            Debug.WriteLine("");

            // instantiating a class on another assembly
            ClassOnAnotherAssembly anotherClass = new ClassOnAnotherAssembly();

            //////////////////////////////
            // accessing property on class
            _ = anotherClass.DummyProperty;

            /////////////////////////////////////////////////////////////////////////
            // instantiating a class on another assembly with a constructor parameter
            anotherClass = new ClassOnAnotherAssembly(99);

            //////////////////////////////
            // accessing property on class
            _ = anotherClass.DummyProperty;

            /////////////////////////////
            // Reflection Tests
            ReflectionTests();

            ///////////////////////////////////////////////////////////////////
            // Generics Tests
            _ = new GenericClassTests();

            /////////////////////////////////////////////////////////////////// 
            // Miscelaneous Tests
            MiscelaneousTests();

            Debug.WriteLine("Exiting TestNFApp");
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
            Debug.WriteLine("++++++++++++++++++++++");
            Debug.WriteLine("++ Reflection tests ++");
            Debug.WriteLine("++++++++++++++++++++++");
            Debug.WriteLine("");

            // Get the type of MyClass1.
            Type myType = typeof(MyClass1);

            // Display the attributes of MyClass1.
            object[] myAttributes = myType.GetCustomAttributes(true);

            Debug.WriteLine("");
            Debug.WriteLine($"'{myType.Name}' type has {myAttributes.Length} custom attributes");

            if (myAttributes.Length > 0)
            {
                Debug.WriteLine("");
                Debug.WriteLine($"The attributes for the class '{myType.Name}' are:");

                for (int j = 0; j < myAttributes.Length; j++)
                {
                    Debug.WriteLine($"  {myAttributes[j]}");
                }
            }

            // Get the methods associated with MyClass1.
            MemberInfo[] myMethods = myType.GetMethods();

            Debug.WriteLine("");
            Debug.WriteLine($"'{myType.Name}' type has '{myMethods.Length}' methods");

            // Display the attributes for each of the methods of MyClass1.
            for (int i = 0; i < myMethods.Length; i++)
            {
                var methodName = myMethods[i].Name;

                Debug.WriteLine("");
                Debug.WriteLine($"Getting custom attributes for '{methodName}'");

                myAttributes = myMethods[i].GetCustomAttributes(true);

                if (myAttributes.Length > 0)
                {
                    Debug.WriteLine("");
                    Debug.WriteLine($"'{methodName}' method has {myAttributes.Length} custom attributes");

                    Debug.WriteLine("");
                    Debug.WriteLine($"The attributes for the method '{methodName}' of class '{myType.Name}' are:");

                    for (int j = 0; j < myAttributes.Length; j++)
                    {
                        Debug.WriteLine("");
                        Debug.WriteLine($"  {myAttributes[j]}");

                        // check if the method has IgnoreAttribute
                        if (myAttributes[j] is IgnoreAttribute)
                        {
                            Debug.WriteLine($"  >>>>>>> {methodName} has 'IgnoreAttribute' attribute");
                        }

                        // check if the method has DataRowAttribute
                        if (myAttributes[j] is DataRowAttribute)
                        {
                            Debug.WriteLine($"  >>>>>>> {methodName} has 'DataRowAttribute' attribute");

                            DataRowAttribute attDataRow = (DataRowAttribute)myAttributes[j];

                            int index = 0;

                            foreach (var dataRow in attDataRow.Arguments)
                            {
                                Console.WriteLine($"          DataRowAttribute.Arg[{index++}] has: {dataRow}");
                            }
                        }

                        // check if the method has ComplexAttribute
                        if (myAttributes[j] is ComplexAttribute)
                        {
                            Debug.WriteLine($"  >>>>>>> {methodName} has 'ComplexAttribute' attribute");

                            ComplexAttribute attDataRow = (ComplexAttribute)myAttributes[j];

                            Console.WriteLine($"          ComplexAttribute.Max is {attDataRow.Max}");
                            Console.WriteLine($"          ComplexAttribute.B   is {attDataRow.B}");
                            Console.WriteLine($"          ComplexAttribute.S   is {attDataRow.S}");
                        }

                    }
                }
            }

            // display the custom attributes with constructor
            var myClass = new MyClass1();

            var myFieldAttributes = myClass.GetType().GetField("MyPackedField").GetCustomAttributes(true);

            Debug.WriteLine("");
            Debug.WriteLine($"The custom attributes of field 'MyPackedField' are:");

            MaxAttribute attMax = (MaxAttribute)myFieldAttributes[0];
            Debug.WriteLine("");
            Debug.WriteLine($"MaxAttribute value is: 0x{attMax.Max.ToString("X8")}");

            AuthorAttribute attAuthor = (AuthorAttribute)myFieldAttributes[1];
            Debug.WriteLine("");
            Debug.WriteLine($"AuthorAttribute value is: '{attAuthor.Author}'");

            Debug.WriteLine("");
            Debug.WriteLine("+++ReflectionTests completed");
            Debug.WriteLine("");
        }
    }
}
