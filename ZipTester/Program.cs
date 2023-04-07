using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.Compression;
using System.Net;
using System.Collections.Concurrent;

namespace ZipTester
{
    internal class Program
    {
        internal struct EntryData
        {
            public string archiveFullName;
            public string entryFullName;
        }

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

            // enumerate files
            int count = 0;
            foreach ( var fInfo in dInfo.EnumerateFiles(zipExtension, SearchOption.AllDirectories) )
            {
                count++;
                Console.WriteLine("[" + count + "] PROCESING : " + fInfo.FullName);

                //SequentialTestEntries(fInfo.FullName);

                ParallelTestEntries(fInfo.FullName);

                //break; // only do 1 file for now.
            }

            Console.WriteLine("finished!");
            Console.ReadLine();
        }

        private static void SequentialTestEntries(string fullName)
        {
            byte[] buffer = new byte[4096];
            int quantityRead;
            long uncompressedLength;
            long quantityAccumulated;

            // test all the zip file contents
            ZipArchive zArchive = ZipFile.OpenRead(fullName);
            foreach (ZipArchiveEntry zArchiveEntry in zArchive.Entries)
            {
                quantityAccumulated = 0;
                uncompressedLength = zArchiveEntry.Length;
                Stream entryStream = zArchiveEntry.Open();
                BufferedStream entryStreamBuf = new BufferedStream(entryStream, 65536);

                while (quantityAccumulated < uncompressedLength)
                {
                    quantityRead = entryStreamBuf.Read(buffer, 0, buffer.Length);
                    quantityAccumulated += quantityRead;
                    if (quantityRead != buffer.Length)
                    {
                        if (quantityAccumulated != uncompressedLength)
                        {
                            // failed to read when should have been able to.
                            Console.WriteLine("    WARNING: failed to read past byte " + quantityAccumulated + " in compressed data for " + zArchiveEntry.FullName);
                            break;
                        }
                    }
                }

                entryStreamBuf.Close();
                entryStream.Close();
            }

            zArchive.Dispose();
        }

        private static void ParallelTestEntries(string fullName)
        {
            var entries = new ConcurrentBag<EntryData>();
            ZipArchive zArchive = ZipFile.OpenRead(fullName);
            foreach (var zEntry in zArchive.Entries)
            {
                entries.Add(new EntryData() { archiveFullName = fullName, entryFullName = zEntry.FullName });
            }
            zArchive.Dispose();

            var failedTests = new ConcurrentBag<string>();
            Parallel.ForEach(entries, zEntryData =>
            {
                string result = TestZipEntry(zEntryData);
                if (result.Length > 0)
                {
                    failedTests.Add(result);
                }
            }
            );

            foreach (var failureMessage in failedTests)
            {
                Console.WriteLine(failureMessage);
            }
        }

        private static string TestZipEntry(EntryData eData)
        {
            string result = "";

            ZipArchive zArchive = ZipFile.OpenRead(eData.archiveFullName);
            ZipArchiveEntry entry = zArchive.GetEntry(eData.entryFullName);

            byte[] buffer = new byte[4096];
            int quantityRead;

            long quantityAccumulated = 0;
            long uncompressedLength = entry.Length;
            Stream entryStream = entry.Open();
            BufferedStream entryStreamBuf = new BufferedStream(entryStream, 65536);

            while (quantityAccumulated < uncompressedLength)
            {
                quantityRead = entryStreamBuf.Read(buffer, 0, buffer.Length);
                quantityAccumulated += quantityRead;
                if (quantityRead != buffer.Length)
                {
                    if (quantityAccumulated != uncompressedLength)
                    {
                        // failed to read when should have been able to.
                        result = "    WARNING: failed to read past byte " + quantityAccumulated + " in compressed data for " + entry.FullName;
                        break;
                    }
                }
            }

            entryStreamBuf.Close();
            entryStream.Close();
            zArchive.Dispose();

            return result;
        }
    }
}
