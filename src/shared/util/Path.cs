using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace shared.util
{
    public static class PathUtilities
    {
        public static bool IsFile(string path)
        {
            if (File.Exists(path))
            {
                FileAttributes attr = File.GetAttributes(path);

                // Check if the Directory flag is NOT set
                if (!attr.HasFlag(FileAttributes.Directory))
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                return true;
            }

            return false;
        }

        public static bool IsDirectory(string path)
        {
            if (path.TakeLast(1).First() == Path.PathSeparator)
            {
                return true;
            }

            return !IsFile(path);
        }

        public static string GetDirectory(string path)
        {
            if (path == null) return string.Empty;

            if (path.TakeLast(1).First() == Path.PathSeparator)
            {
                return path;
            }

            if (IsFile(path))
            {
                return Path.GetDirectoryName(path);
            }

            return path;
        }
    }
}