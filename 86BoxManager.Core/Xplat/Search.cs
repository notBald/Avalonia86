using System.IO;

namespace _86BoxManager.Xplat
{
    public static class Search
    {
        public static string CheckTrail(this string path)
        {
            path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            //To make sure there's a trailing backslash at the end, as other code using these strings expects it!
            if (!path.EndsWith(Path.DirectorySeparatorChar))
            {
                path += Path.DirectorySeparatorChar;
            }
            return path;
        }
    }
}