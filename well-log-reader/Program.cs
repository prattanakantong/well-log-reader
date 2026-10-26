using System;
using System.Runtime.InteropServices;

namespace WellLogReader
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Well Log Reader starting...");
            Console.WriteLine("Runtime: " + RuntimeInformation.FrameworkDescription);
        }
    }
}