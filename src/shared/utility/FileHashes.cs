using System.Security.Cryptography;
namespace shared.utility;

public static class FileHashes
{

    public static async Task<string> HashOpenRead(string filePath)
    {
        byte[] hashBytes = SHA256.HashData(File.OpenRead(filePath));
        return Convert.ToHexString(hashBytes);
    }

    public static async Task<string> HashFs(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.SequentialScan);
        byte[] hashBytes = SHA256.HashData(fs);
        return Convert.ToHexStringLower(hashBytes);
    }
}