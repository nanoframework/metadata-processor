using System.Threading;

namespace GenerationTestNFApp
{
    public class Program
    {
        public static void Main()
        {
            var nativeMethods = new NativeMethodGeneration();
            nativeMethods.Method();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}