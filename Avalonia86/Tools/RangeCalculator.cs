using System;
using System.Collections.Generic;

public static class RangeCalculator
{
    public static uint Calculate(uint baseSize, (string start, string size)[] input,
        bool onlyFirstContinuous = true)
    {
        var ranges = new List<(int start, int end)>();

        // System memory forms the first range, and always starts from 0
        if (baseSize > 0)
            ranges.Add((0, (int)baseSize));

        // EMS memory goes here
        uint extra = 0;

        // Parse input, ignore invalid
        foreach (var (startStr, sizeStr) in input)
        {
            // EMS memory: We ignore adresses and add the size
            if (startStr == null)
            {
                if (uint.TryParse(sizeStr, out uint s))
                    extra += s;

                continue;
            }

            // If the data is corrupt, we ignore
            if (!int.TryParse(startStr, out int start) || start < 0)
                continue;

            // If the data is corrupt, we ignore
            if (!int.TryParse(sizeStr, out int size) || size < 0)
                continue;

            ranges.Add((start, start + size));
        }

        if (ranges.Count == 0)
            return extra;

        // Merge intervals
        ranges.Sort((a, b) => a.start.CompareTo(b.start));

        // Adds the ranges into the pot
        int startIndex = 0;

        // find first range that could include 0
        if (onlyFirstContinuous)
        {
            while (startIndex < ranges.Count && ranges[startIndex].end <= 0)
                startIndex++;

            if (startIndex >= ranges.Count)
                return extra;
        }

        int currentStart = Math.Max(0, ranges[startIndex].start);
        int currentEnd = ranges[startIndex].end;

        // ensure it actually starts at or before 0
        if (currentStart > 0 && onlyFirstContinuous)
            return extra;

        // Adds all ranges in the pot
        long total = 0;

        for (int i = startIndex + 1; i < ranges.Count; i++)
        {
            var (s, e) = ranges[i];

            if (s <= currentEnd)
            {
                // extend continuous range
                currentEnd = Math.Max(currentEnd, e);
            }
            else
            {
                // first gap → stop entirely
                if (onlyFirstContinuous)
                    break;

                total += currentEnd - currentStart;
                currentStart = s;
                currentEnd = e;
            }
        }

        total += currentEnd - currentStart;

        return (uint)(total + extra);
    }
}