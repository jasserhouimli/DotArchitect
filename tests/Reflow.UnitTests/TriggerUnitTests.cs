using Reflow.Modules.Triggers.Services;
using Xunit;

namespace Reflow.UnitTests;

public class TriggerUnitTests
{
    [Theory]
    [InlineData("0 8 * * *", true)]
    [InlineData("* * * * *", true)]
    [InlineData("*/15 9-17 * * 1-5", true)]
    [InlineData("not a cron", false)]
    [InlineData("* * *", false)]
    [InlineData("61 * * * *", false)]
    public void Cron_Validation(string expression, bool valid)
    {
        Assert.Equal(valid, CronSchedule.TryParse(expression, out _));
    }

    [Fact]
    public void Cron_NextOccurrence_Daily()
    {
        var from = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var next = CronSchedule.GetNextOccurrence("0 8 * * *", "UTC", from);

        Assert.Equal(new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Cron_NextOccurrence_RespectsTimezone()
    {
        var from = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var berlin = CronSchedule.GetNextOccurrence("0 8 * * *", "Europe/Berlin", from);
        var utc = CronSchedule.GetNextOccurrence("0 8 * * *", "UTC", from);

        Assert.NotNull(berlin);
        Assert.NotNull(utc);
        Assert.Equal(TimeSpan.FromHours(1), utc.Value - berlin.Value);
    }

    [Theory]
    [InlineData("UTC", true)]
    [InlineData("America/New_York", true)]
    [InlineData("Mars/Olympus", false)]
    public void Timezone_Validation(string timezone, bool valid)
    {
        Assert.Equal(valid, CronSchedule.IsValidTimezone(timezone));
    }

    [Fact]
    public void WebhookToken_GeneratesUniqueUrlSafeTokens()
    {
        var a = WebhookToken.Generate();
        var b = WebhookToken.Generate();

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("+", a);
        Assert.DoesNotContain("/", a);
        Assert.DoesNotContain("=", a);
        Assert.Equal(64, WebhookToken.Hash(a).Length);
    }
}
