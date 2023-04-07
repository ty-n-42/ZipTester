using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZipTester
{
    internal class Program
    {
        private const string testPath = "C:\\Users\\tyn\\Downloads";
        private const string zipExtension = "*.zip";

        static void Main(string[] args)
        {   
            // search the path for zip files
            DirectoryInfo dInfo = new DirectoryInfo(testPath);
            if (dInfo.Exists != true)
            {
                Console.WriteLine("ERROR: path does not exist: " + testPath);
                Environment.Exit(-1);
            }

            // enumerate 
            int count = 0;
            foreach ( var fInfo in dInfo.EnumerateFiles(zipExtension, SearchOption.AllDirectories) )
            {
                count++;
                Console.WriteLine("" + count + ": " + fInfo.FullName);
            }

            Console.WriteLine("finished!");
            Console.ReadLine();
        }
    }
}
