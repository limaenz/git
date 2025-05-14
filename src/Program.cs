using System.IO.Compression;
using System.Security.Cryptography;
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

                if (findNull)
                    chars.Add(letter);

                if (letter == char.MinValue)
                    findNull = true;
            }

            if (chars.Count != 0)
            {
                string content = new([.. chars]);
                Console.Write(content);
            }
        }
        break;

    case "hash-object":
        {
            if (args[1] != "-w" && args[2] is null)
                return;

            var file = args[2];

            using FileStream fileStream = File.OpenRead($"{file}");
            using var sr = new StreamReader(fileStream);

            var contentFile = sr.ReadToEnd();

            string content = $"blob {contentFile.Length}{char.MinValue}{contentFile}";
            byte[] bytes = Encoding.ASCII.GetBytes(content);

            var hash = Convert.ToHexStringLower(SHA1.HashData(bytes));

            string path = $".git/objects/{hash[..2]}/{hash.Substring(2, 38)}";

            Directory.CreateDirectory($".git/objects/{hash[..2]}");

            using FileStream compressedFileStream = File.OpenWrite(path);
            using var ds = new ZLibStream(compressedFileStream, CompressionMode.Compress);
            using var sw = new StreamWriter(ds);

            Console.Write(hash);
        }
        break;

    case "ls-tree":
        {
            if (args[1] != "--name-only" && args[2] is null)
                return;

            var objects = args[2];
            string path = $".git/objects/{objects[..2]}/{objects.Substring(2, 38)}";

            using FileStream compressedFileStream = File.OpenRead(path);
            using var ds = new ZLibStream(compressedFileStream, CompressionMode.Decompress);
            using var sr = new StreamReader(ds);

            var decompressedObject = sr.ReadToEnd();

            byte[] bytes = Encoding.ASCII.GetBytes(decompressedObject);
            var names = new List<string>();
            bool findNull = false;

            for (int i = 0; i < bytes.Length; i++)
            {
                if (findNull)
                {
                    var result = GetFileNamesTree(bytes, i);
                    names = result;
                    break;
                }

                if (bytes[i] is 0)
                    findNull = true;
            }

            foreach (var item in names)
                Console.WriteLine(item);
        }
        break;

    default: throw new ArgumentException($"Unknown command {command}");
}

List<string> GetFileNamesTree(byte[] bytes, int current)
{
    bool isTree = false;
    List<char> chars = [];
    List<string> names = [];

    for (int i = current; i < bytes.Length; i++)
    {
        char letter = ((char)bytes[i]);

        if (bytes[i] is 0 && isTree)
        {
            if (i + 20 < bytes.Length)
                i += 20;

            isTree = false;
            names.Add(new([.. chars]));
            chars = [];
        }

        if (isTree)
            chars.Add(letter);

        if (bytes[i] == 0x20)
            isTree = true;
    }

    return names;
}