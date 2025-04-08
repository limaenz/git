using System;
using System.IO;
using System.IO.Compression;
using System.Text;

if (args.Length < 1)
{
    Console.WriteLine("Please provide a command.");
    return;
}

string command = args[0];

switch (command)
{
    case "init":

        Directory.CreateDirectory(".git");
        Directory.CreateDirectory(".git/objects");
        Directory.CreateDirectory(".git/refs");
        File.WriteAllText(".git/HEAD", "ref: refs/heads/main\n");
        Console.WriteLine("Initialized git directory");
        break;

    case "cat-file":
        {
            if (args[1] != "-p" && args[2] is null)
                return;

            var objects = args[2];
            string path = $".git/objects/{objects[..2]}/{objects.Substring(2, 38)}";

            using FileStream compressedFileStream = File.OpenRead(path);
            using var ds = new ZLibStream(compressedFileStream, CompressionMode.Decompress);
            using var sr = new StreamReader(ds);

            var decompressedObject = sr.ReadToEnd();

            byte[] bytes = Encoding.ASCII.GetBytes(decompressedObject);
            var chars = new List<char>();

            bool findNull = false;
            for (int i = 0; i < bytes.Length; i++)
            {
                char letter = ((char)bytes[i]);
                if (letter == char.MinValue)
                    findNull = true;

                if (findNull)
                    chars.Add(letter);
            }

            if (chars.Count != 0)
            {
                string content = new([.. chars]);
                Console.WriteLine(content);
            }
        }
        break;

    default: throw new ArgumentException($"Unknown command {command}");
}
