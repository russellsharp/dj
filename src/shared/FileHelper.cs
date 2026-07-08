using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Buffers;
using System.Diagnostics;
using System.Text;
namespace shared;

public enum FileAccessResult
{
    Available,
    DoesNotExist,
    Locked,
    UnauthorizedAccessException
}

public static class FileHelper
{

    public static bool CanAccess(string filePath)
    {
        return CanAccessFile(filePath) == FileAccessResult.Available;
    }

    public static FileAccessResult CanAccessFile(string filePath, FileAccess accessType = FileAccess.Read)
    {
        // First check if the file even exists
        if (!File.Exists(filePath)) return FileAccessResult.DoesNotExist;

        try
        {
            // Attempt to open the file with the requested access mode
            using (FileStream fs = File.Open(filePath, FileMode.Open, accessType, FileShare.ReadWrite))
            {
                return FileAccessResult.Available;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Thrown if the OS denies permissions (e.g., Read-Only file or Admin-only folder)
            return FileAccessResult.UnauthorizedAccessException;
        }
        catch (IOException)
        {
            // Thrown if the file is locked by another process or an I/O error occurs
            return FileAccessResult.Locked;
        }
    }

    public static string AccessMessage(this FileAccessResult result, string path, FileAccess access)
    {
        return result switch
        {
            FileAccessResult.Available => $"File is available for access: {access}, {path}",
            FileAccessResult.DoesNotExist => $"File does not exist: {access}, {path}",
            FileAccessResult.Locked => $"File is locked by another process: {access}, {path}",
            FileAccessResult.UnauthorizedAccessException => $"File cannot be accessed with requested access: {access}, {path}",
            _ => "Unsupported result type."
        };
    }

    public static async Task<bool> CreateFile(string filePath, long lengthKBytes, byte filler)
    {
        var fileDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath))
            ?? throw new InvalidOperationException("Unable to determine file directory");

        Directory.CreateDirectory(fileDirectory);

        long totalBytes = lengthKBytes * 1024;

        int chunkSize = 4096;

        byte[] buffer = new byte[chunkSize];

        buffer = Enumerable.Repeat(filler, chunkSize).ToArray();

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        long bytesWritten = 0;
        while (bytesWritten < totalBytes)
        {
            int bytesLeft = (int)(totalBytes - bytesWritten);
            int bytesToWrite = Math.Min(chunkSize, bytesLeft);

            await fs.WriteAsync(buffer.AsMemory(0, bytesToWrite));
            bytesWritten += bytesToWrite;
        }
        fs.Close();
        return File.Exists(filePath);
    }

    public static async Task<shared.data.File> PathToFile(string path)
    {
        var fileInfo = new FileInfo(path);

        // var fileHash = await shared.FileHashes.HashFs(path);

        var fullPath = Path.GetFullPath(path);

        return new()
        {
            path = fullPath,
            path_hash = HashString(fullPath),
            date_modified = File.GetLastWriteTimeUtc(path),
            date_created = File.GetCreationTimeUtc(path),
            size = fileInfo.Length,
            extension = Path.GetExtension(fullPath),
            hash = "", // fileHash.ToString(),
            attributes = File.GetAttributes(path).ToString()
        };
    }

    public static string HashString(string subject)
    {
        byte[] unicodeBytes = Encoding.Unicode.GetBytes(subject);
        byte[] pathHash = SHA256.HashData(unicodeBytes);
        return Convert.ToHexString(pathHash);
    }
}
