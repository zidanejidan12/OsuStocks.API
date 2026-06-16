using OsuStocks.Application.Features.DailyLogin;
using Xunit;

namespace OsuStocks.Application.UnitTests.Features.DailyLogin;

public sealed class DailyLoginClockTests
{
    [Fact]
    public void ServerToday_IsTheUtcCalendarDate()
    {
        // Bracket the call so the assertion is robust even if it straddles a UTC midnight tick.
        var before = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var today = DailyLoginClock.ServerToday();
        var after = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        Assert.True(today == before || today == after, $"Expected {before} or {after} but got {today}.");
    }
}
