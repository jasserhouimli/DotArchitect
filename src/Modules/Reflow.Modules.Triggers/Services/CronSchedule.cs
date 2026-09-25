using Cronos;

namespace Reflow.Modules.Triggers.Services;

public static class CronSchedule
{
    public static bool TryParse(string expression, out string error)
    {
        try
        {
            CronExpression.Parse(expression, CronFormat.Standard);
            error = string.Empty;
            return true;
        }
        catch (CronFormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool IsValidTimezone(string timezone)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static DateTime? GetNextOccurrence(string expression, string timezone, DateTime fromUtc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var cron = CronExpression.Parse(expression, CronFormat.Standard);
        return cron.GetNextOccurrence(new DateTimeOffset(fromUtc, TimeSpan.Zero), tz)?.UtcDateTime;
    }
}
