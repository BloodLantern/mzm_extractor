using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace MzmExtractor
{
    class Program
    {
        const string romPath = "mzm_us_baserom.gba", databasePath = "database.txt", dataPath = "data";

        static void Main(string[] args)
        {
            CheckFileExists(romPath);
            CheckFileExists(databasePath);

            Thread deleteOldFilesThread = null;

            if (Directory.Exists(dataPath))
            {
                deleteOldFilesThread = new(
                    () =>
                    {
                        Console.WriteLine("Deleting old files...");
                        Directory.Delete(dataPath, true);
                    }
                );
                deleteOldFilesThread.Start();
            }

            int parallelThreads = Environment.ProcessorCount;
            Console.WriteLine($"Using {parallelThreads} parallel threads.");

            string[] database = File.ReadAllLines(databasePath);
            database = database.Where(line => !(line == string.Empty || (line[0] is '\n' or '#'))).ToArray();

            deleteOldFilesThread?.Join();

            Thread[] threads = new Thread[parallelThreads];
            int dataBlockLength = database.Length / parallelThreads;
            string[] dataBlock;

            Console.WriteLine("Extracting data...");
            for (int i = 0; i < parallelThreads; i++)
            {
                dataBlock = new string[dataBlockLength];
                Array.Copy(database, dataBlockLength * i, dataBlock, 0, dataBlockLength);

                Thread t = threads[i] = new Thread(ExtractData);
                Console.WriteLine($"Starting thread #{i}.");
                t.Start(dataBlock);
            }

            // Make the main thread extract the remaining data
            int remainingDataLength = database.Length % parallelThreads;
            dataBlock = new string[remainingDataLength];
            Array.Copy(database, database.Length - remainingDataLength, dataBlock, 0, remainingDataLength);
            ExtractData(dataBlock);

            foreach (Thread t in threads)
                t.Join();
        }

        static void CheckFileExists(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"File {path} does not exist.");
                Environment.Exit(1);
            }
        }

        static void ExtractData(object dataBlock)
        {
            using FileStream romFileStream = new(romPath, FileMode.Open, FileAccess.Read);
            using BinaryReader rom = new(romFileStream);

            foreach (string line in (string[]) dataBlock)
            {
                string[] split = line.Split(';');

                string name = split[0];
                Console.WriteLine("Extracting " + name);

                string address = split[2];
                // Remove leading 0x or 0X
                if (address.StartsWith("0x") || address.StartsWith("0X"))
                    address = address[2..];

                romFileStream.Seek(int.Parse(address, NumberStyles.HexNumber), SeekOrigin.Begin);

                int size = int.Parse(split[3]);

                string filePath = Path.Combine(dataPath, name);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                using FileStream stream = new(filePath, FileMode.Append, FileAccess.Write);
                using BinaryWriter writer = new(stream);

                int length = int.Parse(split[1]);
                for (int i = 0; i < length; i++)
                    writer.Write(rom.ReadBytes(size));
            }
        }
    }
}