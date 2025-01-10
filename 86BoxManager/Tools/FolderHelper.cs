using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Threading;

namespace _86BoxManager.Tools
{
    internal static class FolderHelper
    {
        public static void CopyFilesAndFolders(string sourceDir, string destinationDir, int copyDepth, Action<int> progressCallback = null)
        {
            if (copyDepth < 0)
            {
                throw new ArgumentException("Copy depth must be zero or greater.", nameof(copyDepth));
            }

            if (sourceDir == null || destinationDir == null)
            {
                throw new ArgumentNullException();
            }

            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            sourceDir = Path.GetFullPath(sourceDir);
            destinationDir = Path.GetFullPath(destinationDir);


            // Generate the list of all operations
            var copyOperations = new List<CopyOperation>();
            var co = new CopyOperation() { Source = null, Destination = destinationDir };

            copyOperations.Add(co);

            long totalSizeEstimate = GenerateCopyOperations(sourceDir, destinationDir, copyDepth, 0, copyOperations);           

            long totalCopied = 0;
            try
            {
                // Perform the copy and mark operations as completed
                foreach (var operation in copyOperations)
                {
                    if (operation.IsFile)
                    {
                        CopyFileWithProgress(operation.Source, operation.Destination, operation.Size, ref totalCopied, totalSizeEstimate, progressCallback);
                    }
                    else if (!Directory.Exists(operation.Destination))
                    {
                        Directory.CreateDirectory(operation.Destination);
                    }
                    operation.Completed = true;
                }

                // Verify the copied structure
                VerifyCopiedStructure(copyOperations);
            }
            catch (Exception ex)
            {
                // Undo completed operations
                bool failed = UndoCopyOperations(copyOperations);

                if (failed)
                    throw new IOException("Copy operation failed but was not rolled back.", ex);

                throw new IOException("Copy operation failed and was rolled back.", ex);
            }
        }

        private static long GenerateCopyOperations(string sourceDir, string destinationDir, int maxDepth, int currentDepth, List<CopyOperation> operations)
        {
            long total = 0;

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileInfo = new FileInfo(file);
                var co = new CopyOperation
                {
                    Source = file,
                    Destination = Path.Combine(destinationDir, Path.GetFileName(file)),
                    IsFile = true,
                    Size = fileInfo.Length
                };
                operations.Add(co);

                total += co.Size;
            }

            if (currentDepth < maxDepth)
            {
                foreach (var dir in Directory.GetDirectories(sourceDir))
                {
                    var destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
                    operations.Add(new CopyOperation
                    {
                        Source = dir,
                        Destination = destDir,
                        IsFile = false
                    });

                    total += GenerateCopyOperations(dir, destDir, maxDepth, currentDepth + 1, operations);
                }
            }

            return total;
        }

        private static void CopyFileWithProgress(string sourceFile, string destinationFile, long fileSize, ref long totalCopied, long totalSizeEstimate, Action<int> progressCallback)
        {
            const int bufferSize = 81920;
            byte[] buffer = new byte[bufferSize];
            long fileCopied = 0;

            if (File.Exists(destinationFile))
                throw new IOException("Destination file already exists");

            try
            {
                using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
                using (FileStream destStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write))
                {
                    int bytesRead;
                    while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destStream.Write(buffer, 0, bytesRead);
                        fileCopied += bytesRead;
                        totalCopied += bytesRead;

                        if (totalSizeEstimate > 0)
                        {
                            int progress = (int)((totalCopied * 100) / totalSizeEstimate);
                            if (progressCallback != null)
                                progressCallback?.Invoke(progress);
                        }
                    }
                }
            }
            catch
            {
                // Remove partially copied file
                if (File.Exists(destinationFile))
                {
                    File.Delete(destinationFile);
                }
                throw;
            }
        }

        private static bool UndoCopyOperations(List<CopyOperation> operations)
        {
            bool undo_failed = false;

            for (int c=operations.Count - 1; c >= 0; c--)
            {
                var operation = operations[c];

                if (operation.Completed)
                {
                    try
                    {
                        if (operation.IsFile && File.Exists(operation.Destination))
                        {
                            File.Delete(operation.Destination);
                        }
                        else if (!operation.IsFile && Directory.Exists(operation.Destination))
                        {
                            Directory.Delete(operation.Destination, true);
                        }
                    } catch { undo_failed = true; }
                }
            }

            return undo_failed;
        }

        private static void VerifyCopiedStructure(List<CopyOperation> operations)
        {
            foreach (var operation in operations)
            {
                if (operation.IsFile)
                {
                    if (!File.Exists(operation.Destination))
                    {
                        throw new IOException($"Expected file missing: {operation.Destination}");
                    }
                }
                else
                {
                    if (!Directory.Exists(operation.Destination))
                    {
                        throw new IOException($"Expected directory missing: {operation.Destination}");
                    }
                }
            }
        }

        private class CopyOperation
        {
            public string Source { get; set; }
            public string Destination { get; set; }
            public bool IsFile { get; set; }
            public long Size { get; set; }
            public bool Completed { get; set; }
        }


        //public static void CopyFilesAndFolders(string sourceDir, string destinationDir, int copyDepth)
        //{
        //    if (copyDepth < 0)
        //    {
        //        throw new ArgumentException("Copy depth must be zero or greater.", nameof(copyDepth));
        //    }

        //    if (sourceDir == null || destinationDir == null)
        //        throw new ArgumentNullException();


        //    // Ensure the source directory exists
        //    if (!Directory.Exists(sourceDir))
        //    {
        //        throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        //    }

        //    sourceDir = Path.GetFullPath(sourceDir);

        //    // Create the destination directory
        //    if (!Directory.Exists(destinationDir))
        //        Directory.CreateDirectory(destinationDir);

        //    destinationDir = Path.GetFullPath(destinationDir);

        //    // Start the copy process
        //    CopyDirectory(sourceDir, destinationDir, copyDepth, 0);
        //}

        //private static void CopyDirectory(string sourceDir, string destinationDir, int maxDepth, int currentDepth)
        //{
        //    // Copy all files in the current directory
        //    foreach (var file in Directory.GetFiles(sourceDir))
        //    {
        //        string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
        //        File.Copy(file, destFile);
        //    }

        //    // If the current depth is less than the max depth, copy subdirectories
        //    if (currentDepth < maxDepth)
        //    {
        //        foreach (var dir in Directory.GetDirectories(sourceDir))
        //        {
        //            string destDir = Path.Combine(destinationDir, Path.GetFileName(dir));
        //            Directory.CreateDirectory(destDir);

        //            // Recursively copy the contents of the subdirectory
        //            CopyDirectory(dir, destDir, maxDepth, currentDepth + 1);
        //        }
        //    }
        //}

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
