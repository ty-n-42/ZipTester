using System;
using System.IO;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;

namespace ZipTester
{
    internal class Program
    {
        // class code copied from: https://stackoverflow.com/questions/3670057/does-console-writeline-block
        internal static class NonBlockingConsole // provides asynchronous Console writing
        {
            private static BlockingCollection<string> m_Queue = new BlockingCollection<string>();

            static NonBlockingConsole()
            {
                var thread = new Thread(
                  () =>
                  {
                      while (true)
                      {
                          Console.WriteLine(m_Queue.Take());
                      }
                  });
                thread.IsBackground = true;
                thread.Start();
            }

            public static void WriteLine(string value)
            {
#if DEBUG
                m_Queue.Add(value);
#endif
            }

            public static void Flush()
            {
                int sleepDuration = 1000;
                Thread.Sleep(sleepDuration); // wait a moment for 
                while (m_Queue.Count > 0)
                {
                    sleepDuration = Math.Min(sleepDuration / 2, 2);
                    Thread.Sleep(sleepDuration);
                }
            }
        }

        internal struct EntryData // structure to hold zip file entry identifying information - for passing to threads executing in parallel
        {
            public string archiveFullName;
            public string entryFullName;
        }

        private const string testPath = "C:\\test"; // top folder path for searching for zip files
        private const string zipExtension = "*.zip"; // extension to filter file system files to identify zip files
        private const int readSize = 1024 * 64; // memory size for reading from zip file entry stream
        private const int bufferSize = 1024 * 1024; // stream buffer size for reading zip file entries 
        private const long compressedLengthBoundary = 1024 * 512; // minimum size for compressed zip file entry to be processed in parallel

        static void Main(string[] args)
        {   
            DirectoryInfo dInfo = new DirectoryInfo(testPath);
            if (dInfo.Exists != true)
            {
                Console.WriteLine("ERROR: path does not exist: " + testPath);
                Environment.Exit(-1);
            }
            Console.WriteLine("START: " + dInfo.FullName);

            // search the path for zip files
            long count = 0;
            Parallel.ForEach(dInfo.EnumerateFiles(zipExtension, SearchOption.AllDirectories), fInfo =>
            { // process each file in parallel
                NonBlockingConsole.WriteLine("[" + (++count).ToString("N0") + "] PROCESING : " + fInfo.FullName);

                ZipArchive zArchive = ZipFile.OpenRead(fInfo.FullName);

                // filter the zip file entries for items suitable for parallel processing - large compressed size
                System.Collections.Generic.IEnumerable<EntryData> filteredEntries = zArchive.Entries
                    .Where<ZipArchiveEntry>(entry => entry.CompressedLength >= compressedLengthBoundary)
                    .Select(entry => new EntryData { archiveFullName = fInfo.FullName, entryFullName = entry.FullName });
                var entriesForParallel = new ConcurrentBag<EntryData>(filteredEntries);

                // execute independent threads for sequential and for parallel processing
                Parallel.Invoke(
                    () =>
                    { // process sequentially
                        byte[] buffer = new byte[readSize];
                        int quantityRead;
                        long quantityAccumulated;
                        long uncompressedLength;
                        Stream entryStream;
                        BufferedStream entryStreamBuf;

                        // process items suitable for sequential processing - small compressed size
                        foreach (var entry in zArchive.Entries.Where<ZipArchiveEntry>(entry => entry.CompressedLength < compressedLengthBoundary))
                        {
                            uncompressedLength = entry.Length;
                            entryStream = entry.Open();
                            entryStreamBuf = new BufferedStream(entryStream, bufferSize);
                            quantityAccumulated = 0;
                            NonBlockingConsole.WriteLine("  READING: (s_" + entry.CompressedLength.ToString("N0") + "b) " + entry.FullName);
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
                        }
                    },
                    () =>
                    { // process in parallel
                        Parallel.ForEach(entriesForParallel, entry =>
                        {
                            TestZipEntry(entry, true);
                        });
                    }
                    );

                zArchive.Dispose();
            
            });

            NonBlockingConsole.Flush();
            Console.WriteLine("END: processed " + count + " zip files");
            Console.ReadLine();
        }

        private static void TestZipEntry(EntryData eData, bool isParallel)
        {
            ZipArchive zArchive = ZipFile.OpenRead(eData.archiveFullName);
            ZipArchiveEntry entry = zArchive.GetEntry(eData.entryFullName);

            byte[] buffer = new byte[readSize];
            int quantityRead;

            long quantityAccumulated = 0;
            long uncompressedLength = entry.Length;
            Stream entryStream = entry.Open();
            BufferedStream entryStreamBuf = new BufferedStream(entryStream, bufferSize);

            NonBlockingConsole.WriteLine("  READING: (" + (isParallel ? "p_" : "s_") + entry.CompressedLength.ToString("N0") + "b) " + entry.FullName);
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

            return;
        }
    }
}
