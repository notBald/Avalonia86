using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime;
using System.Threading;

namespace _86BoxManager.Tools
{
    internal static class FolderHelper
    {
        public static void CopyFilesAndFolders(string sourceDir, string destinationDir, int copyDepth)
        {
            if (copyDepth < 0)
            {
                throw new ArgumentException("Copy depth must be zero or greater.", nameof(copyDepth));
            }

            if (sourceDir == null || destinationDir == null)
                throw new ArgumentNullException();
            

            // Ensure the source directory exists
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            sourceDir = Path.GetFullPath(sourceDir);

            // Create the destination directory
            if (!Directory.Exists(destinationDir))
                Directory.CreateDirectory(destinationDir);

            destinationDir = Path.GetFullPath(destinationDir);

            // Start the copy process
            CopyDirectory(sourceDir, destinationDir, copyDepth, 0);
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, int maxDepth, int currentDepth)
        {
            // Copy all files in the current directory
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile);
            }

            // If the current depth is less than the max depth, copy subdirectories
            if (currentDepth < maxDepth)
            {
                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                    Directory.CreateDirectory(destDir);

                    // Recursively copy the contents of the subdirectory
                    CopyDirectory(dir, destDir, maxDepth, currentDepth + 1);
                }
            }
        }

        public static void SearchFolders(
        string filepath,
        string filename,
        Action isDoneCallback,
        ConcurrentBag<string> concurrentList,
        Action heartbeatCallback,
        CancellationToken? token = null)
        {
            const int MaxDepth = 20; // Set recursion depth limit

            // Use ThreadPool to start the job
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var lastHeartbeatTime = DateTime.UtcNow;

                try
                {
                    SearchDirectory(filepath, 0);
                }
                catch (Exception)
                {
                    //Console.WriteLine($"An error occurred: {ex.Message}");
                }
                finally
                {
                    // Invoke the completion callback
                    isDoneCallback?.Invoke();
                }

                // Local function to search directories recursively
                void SearchDirectory(string path, int depth)
                {
                    if (depth > MaxDepth || token.HasValue && token.Value.IsCancellationRequested)
                    {
                        return; // Stop recursion if max depth is reached
                    }

                    try
                    {
                        // Check if the file exists in the current directory
                        var files = Directory.GetFiles(path, filename);
                        if (files.Length > 0)
                        {
                            concurrentList.Add(path);

                            // Call the heartbeat callback if 10ms has passed since the last call
                            var now = DateTime.UtcNow;
                            if ((now - lastHeartbeatTime).TotalMilliseconds >= 75)
                            {
                                heartbeatCallback?.Invoke();
                                lastHeartbeatTime = now;
                            }
                        }

                        // Recursively search subdirectories
                        foreach (var directory in Directory.GetDirectories(path))
                        {
                            var di = new DirectoryInfo(directory);
                            if ((di.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                                continue;

                            SearchDirectory(directory, depth + 1);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories we don't have permission to access
                    }
                    catch (Exception)
                    {
                        // Skip other errors too
                    }
                }
            });
        }

        public static bool IsDirectChild(string parent, string child)
        {
            if (!string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(child) &&
                Directory.Exists(parent) && Directory.Exists(child))
            {
                parent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string parentDir = GetParentFolderPath(child);

                if (parentDir != null)
                {
                    parentDir = parentDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    return (parentDir.Equals(parent, StringComparison.OrdinalIgnoreCase));
                }
            }

            return false;
        }

        public static string GetParentFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new ArgumentException("The folder path cannot be null or empty.", nameof(folderPath));
            }

            // Normalize the path to remove any trailing directory separators
            folderPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Find the last directory separator
            int lastSeparatorIndex = folderPath.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });

            if (lastSeparatorIndex == -1)
            {
                // No parent directory found
                return null;
            }

            // Return the parent directory path
            return folderPath.Substring(0, lastSeparatorIndex);
        }

        public static DateTime FetchACreationDate(string path, string filename = "86box.cfg")
        {
            try 
            {
                if (!Directory.Exists(path))
                    return DateTime.Now;

                string filePath = Path.Combine(path, filename);

                if (File.Exists(filePath))
                    return File.GetCreationTime(filePath);

                return Directory.GetCreationTime(path);
            }
            catch { return DateTime.Now; }
        }

        public static string EnsureUniqueFolderName(string path, string folderName)
        {
            if (folderName == null)
            {
                throw new ArgumentNullException(nameof(folderName), "Folder name cannot be null.");
            }

            // Remove problematic characters from the folder name
            folderName = RemoveProblematicPathCharacters(folderName);

            // Autogenerate folder name if it's whitespace
            if (string.IsNullOrWhiteSpace(folderName))
            {
                folderName = "VM";
            }

            string fullPath = Path.Combine(path, folderName);
            int counter = 1;

            // Alter the folder name until a free name is found
            while (Directory.Exists(fullPath))
            {
                folderName = $"{folderName}_{counter}";
                fullPath = Path.Combine(path, folderName);
                counter++;
            }

            return fullPath;
        }

        public static string RemoveProblematicPathCharacters(string folderName)
        {
            // Define the set of invalid characters for folder names
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Replace each invalid character with an underscore
            foreach (char c in invalidChars)
            {
                folderName = folderName.Replace(c, '_');
            }

            return folderName;
        }
    }
}
