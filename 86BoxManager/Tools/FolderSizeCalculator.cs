using System;
using System.IO;


namespace _86BoxManager.Tools
{
    public class FolderSizeCalculator
    {
        public static string GetFolderSizeAsStr(string folderPath)
        {
            try
            {
                var size = GetFolderSize(folderPath);
                return ConvertBytesToReadableSize(size);
            }
            catch { return "Error"; }
        }

        public static string ConvertBytesToReadableSize(long bytes)
        {
            const int scale = 1024;
            string[] units = { "Bytes", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= scale && unitIndex < units.Length - 1)
            {
                size /= scale;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        public static long GetFolderSize(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The directory '{folderPath}' does not exist.");
            }

            long totalSize = 0;

            // Get the size of files in the root folder
            totalSize += GetFilesSize(folderPath);

            // Get the size of files in the first level of subfolders
            foreach (var subfolder in Directory.GetDirectories(folderPath))
            {
                totalSize += GetFilesSize(subfolder);

                // Get the size of files in the second level of subfolders
                foreach (var subSubfolder in Directory.GetDirectories(subfolder))
                {
                    totalSize += GetFilesSize(subSubfolder);
                }
            }

            return totalSize;
        }

        private static long GetFilesSize(string folderPath)
        {
            long size = 0;
            foreach (var file in Directory.GetFiles(folderPath))
            {
                FileInfo fileInfo = new FileInfo(file);
                size += fileInfo.Length;
            }
            return size;
        }
    }
}
