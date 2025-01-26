using System;

namespace Avalonia86.Linux;

public class AppImageInfo
{
    public readonly string Arch;
    public readonly string Name;
    public readonly string Version;

    public AppImageInfo(string arch, string name, string version)
    {
        Arch = arch;
        Name = name;
        Version = version;
    }

    public override string ToString()
    {
        return $"{Name} {Version} {Arch}";
    }

    public static int[] ParseVersion(string input)
    {
        const int MAX_NUM = 3;
        int[] result = new int[MAX_NUM + 1];
        for (int c = 0; c < MAX_NUM + 1; c++)
            result[c] = -1;
        if (input == null)
            return result;

        ReadOnlySpan<char> span = input.AsSpan();
        int dotCount = 0;
        int index = 0;

        while (index < span.Length)
        {
            if (char.IsDigit(span[index]))
            {
                int start = index;
                while (index < span.Length && char.IsDigit(span[index]))
                    index++;

                if (dotCount < MAX_NUM && (start == 0 || span[start - 1] == '.'))
                    result[dotCount] = int.Parse(span.Slice(start, index - start));
            }
            else if (span[index] == '.')
            {
                dotCount++;
                index++;
            }
            else if (span[index] == '-' && index + 1 < span.Length && span[index + 1] == 'b')
            {
                index += 2;
                while (index < span.Length && !char.IsDigit(span[index]))
                    index++;

                if (index < span.Length && char.IsDigit(span[index]))
                {
                    int start = index;
                    while (index < span.Length && char.IsDigit(span[index]))
                        index++;

                    result[MAX_NUM] = int.Parse(span.Slice(start, index - start));
                }
                break;
            }
            else
            {
                index++;
            }
        }

        if (result[0] != -1)
        {
            for (int c = 1; c < MAX_NUM; c++)
            {
                if (result[c] == -1)
                    result[c] = 0;
            }
        }

        return result;
    }
}
