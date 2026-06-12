using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    public static FileAccessResult CanAccessFile(string filePath, FileAccess accessType)
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
}