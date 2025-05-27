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

    case "write-tree":
        {
            var currentDirectory = Directory.GetCurrentDirectory();

            GetTreeObject(currentDirectory);

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

string GetBlobObject(string file)
{
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

    return hash;
}

void GetTreeObject(string currentDirectory)
{
    int contentSize = 0;
    var directories = Directory.GetDirectories(currentDirectory);
    var currentFiles = Directory.GetFiles(currentDirectory);
    var objects = new List<(string hash, string path)>();

    foreach (var directory in directories)
    {
        if (directory.Contains('.'))
            continue;

        string path = $"{directory}";
        var currentDirectories = Directory.GetDirectories(path);

        if (currentDirectories.Length != 0)
            GetTreeObject(path);

        var files = Directory.GetFiles(path);

        var blobs = new List<string>();
        int sizeTree = 0;

        foreach (var file in files)
        {
            string blobHash = GetBlobObject(file);
            byte[] blobBytes = Convert.FromHexString(blobHash);

            var sb = new StringBuilder(blobBytes.Length);
            foreach (var item in blobBytes)
                sb.Append(item);

            blobs.Add($"{GetModeBlobObject(file)} {Path.GetFileName(file)}{char.MinValue}{sb}");
            sizeTree += file.Length;
            contentSize += file.Length;
        }

        var content = new StringBuilder($"tree {sizeTree}{char.MinValue}");
        blobs.ForEach(s => content.Append(s));

        byte[] contentBytes = Encoding.ASCII.GetBytes(content.ToString());
        var hashData = Convert.ToHexStringLower(SHA1.HashData(contentBytes));
        string pathHash = $".git/objects/{hashData[..2]}/{hashData.Substring(2, 38)}";
        Console.WriteLine(pathHash);

        objects.Add((hashData, directory));

        if (File.Exists(pathHash))
            continue;

        Directory.CreateDirectory($".git/objects/{hashData[..2]}");

        using FileStream compressedFileStreamTree = File.OpenWrite(pathHash);
        using var libStream = new ZLibStream(compressedFileStreamTree, CompressionMode.Compress);
        using var streamWriter = new StreamWriter(libStream);
    }

    var currentBlobs = new List<string>();

    foreach (var file in currentFiles)
    {
        string blobHash = GetBlobObject(file);
        byte[] hashBytes = Convert.FromHexString(blobHash);

        var sb = new StringBuilder(hashBytes.Length);
        foreach (var item in hashBytes)
            sb.Append(item);

        currentBlobs.Add($"{GetModeBlobObject(file)} {Path.GetFileName(file)}{char.MinValue}{sb}");
    }

    var currentContent = new StringBuilder($"tree {contentSize}{char.MinValue}");
    objects.ForEach(ob =>
    {
        currentContent.Append($"{GetModeBlobObject(ob.path)} {Path.GetFileName(ob.path)}{char.MinValue}{ob.hash}");
    });
    currentBlobs.ForEach(s => currentContent.Append(s));

    byte[] bytes = Encoding.ASCII.GetBytes(currentContent.ToString());
    var hash = Convert.ToHexStringLower(SHA1.HashData(bytes));

    string pathNewObject = $".git/objects/{hash[..2]}/{hash.Substring(2, 38)}";
        Console.WriteLine(pathNewObject);

    if (File.Exists(pathNewObject))
        return;

    var teste = Directory.CreateDirectory($".git/objects/{hash[..2]}");

    using FileStream compressedFileStream = File.OpenWrite(pathNewObject);
    using var ds = new ZLibStream(compressedFileStream, CompressionMode.Compress);
    using var sw = new StreamWriter(ds);
}

string GetModeBlobObject(string path)
{
    if (Directory.Exists(path))
        return "40000";

    var fileInfo = new FileInfo(path);
    if (fileInfo.LinkTarget != null)
        return "120000";

    if ((File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0)
        return "100755";
    else
        return "100644";
}