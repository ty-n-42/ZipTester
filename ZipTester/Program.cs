using System;
using System.IO;
using System.Threading.Tasks;

using System.IO.Compression;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;

namespace ZipTester
{
    internal class Program
    {
        // class code copied from: https://stackoverflow.com/questions/3670057/does-console-writeline-block
        internal static class NonBlockingConsole 
        { 
            private static BlockingCollection<string> m_Queue = new BlockingCollection<string>();

            static NonBlockingConsole()
            {
                var thread = new Thread(
                  () =>
                  {
                      while (true) Console.WriteLine(m_Queue.Take());
                  });
                thread.IsBackground = true;
                thread.Start();
            }

            public static void WriteLine(string value)
            {
                m_Queue.Add(value);
            }

            public static void Flush()
            {
                int sleepDuration = 1000;
                Thread.Sleep(sleepDuration);
                while (m_Queue.Count > 0)
                {
                    sleepDuration = Math.Min(sleepDuration / 2, 2);
                    Thread.Sleep(sleepDuration);
                }
            }
        }

        internal struct EntryData
        {
            public string archiveFullName;
            public string entryFullName;
        }

        private const string testPath = "C:\\Users\\tyn\\Downloads\\HumbleBundle\\page 2\\audio - Music Creators Unlimited Sounds Loops and Instruments";
        //private const string testPath = "C:\\Users\\tyn\\Downloads";
        private const string zipExtension = "*.zip";
        private const int readSize = 4096;
        private const int bufferSize = 262144;

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
            }

            NonBlockingConsole.Flush();
            Console.WriteLine("finished!");
            Console.ReadLine();
        }

        private static void SequentialTestEntries(string fullName)
        {
            byte[] buffer = new byte[readSize];
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
                BufferedStream entryStreamBuf = new BufferedStream(entryStream, bufferSize);

                while (quantityAccumulated < uncompressedLength)
                {
                    quantityRead = entryStreamBuf.Read(buffer, 0, buffer.Length);
                    quantityAccumulated += quantityRead;
                    if (quantityRead != buffer.Length)
                    {
                        if (quantityAccumulated != uncompressedLength)
                        {
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
            ZipArchive zArchive = ZipFile.OpenRead(fullName); // note: something to do with threading invalidates the open file handle in child threads
            foreach (var zEntry in zArchive.Entries)
            {
                entries.Add(new EntryData() { archiveFullName = fullName, entryFullName = zEntry.FullName });
            }
            zArchive.Dispose();

            Parallel.ForEach(entries, zEntryData =>
            {
                TestZipEntry(zEntryData);
            }
            );

        }

        private static string TestZipEntry(EntryData eData)
        {
            string result = "";

            ZipArchive zArchive = ZipFile.OpenRead(eData.archiveFullName);
            ZipArchiveEntry entry = zArchive.GetEntry(eData.entryFullName);

            byte[] buffer = new byte[readSize];
            int quantityRead;

            long quantityAccumulated = 0;
            long uncompressedLength = entry.Length;
            Stream entryStream = entry.Open();
            BufferedStream entryStreamBuf = new BufferedStream(entryStream, bufferSize);

            while (quantityAccumulated < uncompressedLength)
            {
                quantityRead = entryStreamBuf.Read(buffer, 0, buffer.Length);
                quantityAccumulated += quantityRead;
                if (quantityRead != buffer.Length)
                {
                    if (quantityAccumulated != uncompressedLength)
                    {
                        // failed to read when should have been able to.
                        NonBlockingConsole.WriteLine("    WARNING: failed to read past byte " + quantityAccumulated + " in compressed data for " + entry.FullName);
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
