//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;

namespace TestNFApp
{
    public class TestingDelegates
    {
        public TestingDelegates()
        {
            DelegateTests();
            MulticastDelegateTests();
        }

        // Define a delegate
        public delegate void SimpleDelegate(string message);

        // Method that matches the delegate signature
        public void DisplayMessage(string message)
        {
            Console.WriteLine(message);
        }

        // Another method that matches the delegate signature
        public void DisplayUpperCaseMessage(string message)
        {
            Console.WriteLine("Uppercase: " + message.ToUpper());
        }

        // Another method that matches the delegate signature
        public void DisplayLowerCaseMessage(string message)
        {
            Console.WriteLine("Lowercase: " + message.ToLower());
        }

        private void DelegateTests()
        {
            // Instantiate the delegate
            SimpleDelegate del = new SimpleDelegate(DisplayMessage);

            // Call the delegate
            del("Hello, this is a delegate example!");

            // Using delegate with anonymous method
            SimpleDelegate del2 = delegate (string msg)
            {
                Console.WriteLine(msg);
            };

            del2("Hello, this is a delegate example called from an anonymous method!");

            // Using delegate with lambda expression
            SimpleDelegate del3 = (msg) => Console.WriteLine(msg);

            del3("Hello, this is a delegate example called from a lambda expression!");
        }

        private void MulticastDelegateTests()
        {
            // Instantiate the delegate with multiple methods
            SimpleDelegate del = DisplayMessage;
            del += DisplayUpperCaseMessage;
            del += DisplayLowerCaseMessage;

            // Call the multicast delegate
            del("Hello, this is a multicast delegate example!");
        }
    }
}
