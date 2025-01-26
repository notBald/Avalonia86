using System;

namespace Avalonia86.Tools;

public class TimeDifferenceFormatter
{
    public static TimeDifferenceResult FormatTimeDifferenceAccurate(TimeSpan timeDifference, string when_small, string post)
    {
        if (timeDifference.TotalSeconds < 60 && timeDifference.Seconds != 0)
        {
            string second = "second";
            if (timeDifference.Seconds != 1)
                second += 's';

            return new TimeDifferenceResult($"{timeDifference.Seconds}", "", second, timeDifference);
        }

        return FormatTimeDifference(timeDifference, when_small, post);
    }

    public static TimeDifferenceResult FormatTimeDifference(TimeSpan timeDifference, string when_small, string post)
    {
        if (timeDifference.TotalSeconds < 0)
        {
            return new TimeDifferenceResult("Invalid date", " (in the future)", "", timeDifference);
        }

        if (timeDifference.TotalMinutes < 1)
        {
            return new TimeDifferenceResult(when_small, "", "", timeDifference);
        }
        else if (timeDifference.TotalMinutes < 120)
        {
            int minutes = (int)timeDifference.TotalMinutes;
            string result = $"{minutes} minute{(minutes > 1 ? "s" : "")}";
            return new TimeDifferenceResult(result, "", post, timeDifference);
        }
        else if (timeDifference.TotalHours < 48)
        {
            int hours = (int)timeDifference.TotalHours;
            int minutes = timeDifference.Minutes;
            string firstPart = $"{hours} hour{(hours > 1 ? "s" : "")}";
            string andPart = minutes == 0 ? "" : $" and {minutes} minute{(minutes > 1 ? "s" : "")}";
            return new TimeDifferenceResult(firstPart, andPart, post, timeDifference);
        }
        else if (timeDifference.TotalDays < 420)
        {
            int weeks = (int)(timeDifference.TotalDays / 7);
            double days_and_hours = timeDifference.TotalDays % 7;
            int days = (int)(days_and_hours);
            if (weeks > 0)
            {
                string firstPart = $"{weeks} week{(weeks > 1 ? "s" : "")}";
                string andPart = days == 0 ? "" : $" and {days} day{(days > 1 ? "s" : "")}";
                return new TimeDifferenceResult(firstPart, andPart, post, timeDifference);
            }
            else
            {
                string firstPart = $"{days} day{(days > 1 ? "s" : "")}";
                double fractionalPart = days_and_hours - days;
                int hours = (int) (fractionalPart * 24);
                string andPart = hours == 0 ? "" : $" and {hours} hour{(hours > 1 ? "s" : "")}";
                return new TimeDifferenceResult(firstPart, andPart, post, timeDifference);
            }
        }
        else
        {
            int years = (int)(timeDifference.TotalDays / 365.25);
            int weeks = (int)((timeDifference.TotalDays % 365.25) / 7);
            string firstPart = $"{years} year{(years > 1 ? "s" : "")}";
            string andPart = weeks == 0 ? "" : $" and {weeks} week{(weeks > 1 ? "s" : "")}";
            return new TimeDifferenceResult(firstPart, andPart, post, timeDifference);
        }
    }

    public static string ShortFormatTimeDifference(TimeSpan timeDifference, string when_small)
    {
        if (timeDifference.TotalSeconds < 0)
            return "Invalid date (in the future)";

        if (timeDifference.TotalMinutes < 1)
        {
            return when_small;
        }
        else if (timeDifference.TotalMinutes < 60)
        {
            int minutes = (int)timeDifference.TotalMinutes;
            return $"{minutes} minute{(minutes > 1 ? "s" : "")}";
        }
        else if (timeDifference.TotalHours < 24)
        {
            int hours = (int)timeDifference.TotalHours;
            return $"{hours} hour{(hours > 1 ? "s" : "")}";
        }
        else if (timeDifference.TotalDays < 365)
        {
            int days = (int)(timeDifference.TotalDays);
            return $"{days} day{(days > 1 ? "s" : "")}";
        }
        else
        {
            int years = (int)(timeDifference.TotalDays / 365);
            return $"{years} year{(years > 1 ? "s" : "")}";
        }
    }
}

public sealed class TimeDifferenceResult
{
    private readonly string first_part, and_part, post_part;

    public readonly TimeSpan TimeDifference;

    public string Full { get => first_part + and_part + post_part; }
    public string Short { get => first_part + post_part; }

    public TimeDifferenceResult(string first, string and, string post, TimeSpan td)
    {
        first_part = first;
        and_part = and;
        post_part = string.IsNullOrEmpty(post) ? "" : " "+post;
        TimeDifference = td;
    }

    public TimeDifferenceResult() : this("", "", "", TimeSpan.Zero) { }
}
