using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Api.IntegrationTests.Infrastructure;
using OsuStocks.Domain.Entities;
using OsuStocks.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Exercises the notification read/mark endpoints end-to-end against the real Postgres-backed
/// handlers + repositories. The authenticated caller is always <see cref="TestUserId"/>
/// (see <see cref="TestAuthHandler"/>); notifications owned by another user must never be
/// returned or mutated through these endpoints.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NotificationEndpointsTests(PostgresTestcontainerFixture fixture)
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // A fixed instant the seeded CreatedAt values are derived from, so ordering is deterministic.
    private static readonly DateTimeOffset BaseTime =
        new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetNotifications_ReturnsOnlyCallersRows_NewestFirst()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var set = dbContext.Set<Notification>();

            // Caller's notifications: three rows, increasing CreatedAt so the newest is unambiguous.
            set.Add(NewNotification(TestUserId, "PpIncreased", "first", isRead: true, at: BaseTime.AddMinutes(1)));
            set.Add(NewNotification(TestUserId, "TopPlayDetected", "second", isRead: false, at: BaseTime.AddMinutes(2)));
            set.Add(NewNotification(TestUserId, "PpIncreased", "third", isRead: false, at: BaseTime.AddMinutes(3)));

            // Another user's notification must never appear in the caller's feed.
            set.Add(NewNotification(OtherUserId, "PpIncreased", "intruder", isRead: false, at: BaseTime.AddMinutes(4)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ReadItemsAsync(response);
        Assert.Equal(3, items.Count);

        // Newest first: third (t+3), second (t+2), first (t+1). The other user's row is absent.
        Assert.Equal("third", items[0].Title);
        Assert.Equal("second", items[1].Title);
        Assert.Equal("first", items[2].Title);
        Assert.DoesNotContain(items, i => i.Title == "intruder");

        Assert.True(
            items[0].CreatedAt > items[1].CreatedAt && items[1].CreatedAt > items[2].CreatedAt,
            "Notifications should be ordered by createdAt descending.");

        // Body and type round-trip exactly.
        Assert.Equal("PpIncreased", items[0].Type);
        Assert.Equal("body-third", items[0].Body);
        Assert.False(items[0].IsRead);
        Assert.True(items[2].IsRead);
    }

    [Fact]
    public async Task GetNotifications_WithUnreadFilter_ReturnsOnlyUnread()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var set = dbContext.Set<Notification>();

            set.Add(NewNotification(TestUserId, "PpIncreased", "read-1", isRead: true, at: BaseTime.AddMinutes(1)));
            set.Add(NewNotification(TestUserId, "PpIncreased", "unread-1", isRead: false, at: BaseTime.AddMinutes(2)));
            set.Add(NewNotification(TestUserId, "TopPlayDetected", "unread-2", isRead: false, at: BaseTime.AddMinutes(3)));

            // An unread row for another user must not leak through the unread filter.
            set.Add(NewNotification(OtherUserId, "PpIncreased", "other-unread", isRead: false, at: BaseTime.AddMinutes(4)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/v1/notifications?unread=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ReadItemsAsync(response);
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.False(i.IsRead));

        // Newest unread first.
        Assert.Equal("unread-2", items[0].Title);
        Assert.Equal("unread-1", items[1].Title);
        Assert.DoesNotContain(items, i => i.Title == "read-1");
        Assert.DoesNotContain(items, i => i.Title == "other-unread");
    }

    [Fact]
    public async Task GetNotifications_WithPaging_LimitsAndOffsetsResults()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var set = dbContext.Set<Notification>();

            // Five rows with strictly increasing CreatedAt so we can identify each page exactly.
            for (var i = 1; i <= 5; i++)
            {
                set.Add(NewNotification(TestUserId, "PpIncreased", $"n{i}", isRead: false, at: BaseTime.AddMinutes(i)));
            }

            await dbContext.SaveChangesAsync();
        }

        var firstPage = await client.GetAsync("/api/v1/notifications?page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, firstPage.StatusCode);
        var firstItems = await ReadItemsAsync(firstPage);
        Assert.Equal(2, firstItems.Count);
        // Newest first: n5 (t+5) then n4 (t+4).
        Assert.Equal("n5", firstItems[0].Title);
        Assert.Equal("n4", firstItems[1].Title);

        var secondPage = await client.GetAsync("/api/v1/notifications?page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, secondPage.StatusCode);
        var secondItems = await ReadItemsAsync(secondPage);
        Assert.Equal(2, secondItems.Count);
        // Continuing newest-first: n3 (t+3) then n2 (t+2).
        Assert.Equal("n3", secondItems[0].Title);
        Assert.Equal("n2", secondItems[1].Title);
    }

    [Fact]
    public async Task MarkRead_OwnNotification_MarksRowReadAndReturnsSuccess()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid notificationId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notification = NewNotification(TestUserId, "PpIncreased", "to-read", isRead: false, at: BaseTime);
            notificationId = notification.Id;
            dbContext.Set<Notification>().Add(notification);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/api/v1/notifications/{notificationId}/read", content: null);
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Expected 200 or 204 but got {(int)response.StatusCode} {response.StatusCode}.");

        // The persisted row is now read.
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await dbContext.Set<Notification>()
                .AsNoTracking()
                .SingleAsync(n => n.Id == notificationId);

            Assert.True(row.IsRead);
        }
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_Returns404AndLeavesRowUnread()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid otherNotificationId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notification = NewNotification(OtherUserId, "PpIncreased", "not-yours", isRead: false, at: BaseTime);
            otherNotificationId = notification.Id;
            dbContext.Set<Notification>().Add(notification);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsync($"/api/v1/notifications/{otherNotificationId}/read", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // The other user's row must remain untouched (still unread).
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await dbContext.Set<Notification>()
                .AsNoTracking()
                .SingleAsync(n => n.Id == otherNotificationId);

            Assert.False(row.IsRead);
        }
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyCallersUnreadRows()
    {
        await using var factory = new PostgresWebApplicationFactory(fixture);
        using var client = factory.CreateClient();

        Guid callerAlreadyReadId;
        Guid callerUnread1Id;
        Guid callerUnread2Id;
        Guid otherUnreadId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var set = dbContext.Set<Notification>();

            var alreadyRead = NewNotification(TestUserId, "PpIncreased", "already-read", isRead: true, at: BaseTime.AddMinutes(1));
            var unread1 = NewNotification(TestUserId, "PpIncreased", "unread-1", isRead: false, at: BaseTime.AddMinutes(2));
            var unread2 = NewNotification(TestUserId, "TopPlayDetected", "unread-2", isRead: false, at: BaseTime.AddMinutes(3));
            var otherUnread = NewNotification(OtherUserId, "PpIncreased", "other-unread", isRead: false, at: BaseTime.AddMinutes(4));

            callerAlreadyReadId = alreadyRead.Id;
            callerUnread1Id = unread1.Id;
            callerUnread2Id = unread2.Id;
            otherUnreadId = otherUnread.Id;

            set.AddRange(alreadyRead, unread1, unread2, otherUnread);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsync("/api/v1/notifications/read-all", content: null);
        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Expected 200 or 204 but got {(int)response.StatusCode} {response.StatusCode}.");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await dbContext.Set<Notification>()
                .AsNoTracking()
                .ToListAsync();

            // All of the caller's rows are now read.
            Assert.True(rows.Single(n => n.Id == callerAlreadyReadId).IsRead);
            Assert.True(rows.Single(n => n.Id == callerUnread1Id).IsRead);
            Assert.True(rows.Single(n => n.Id == callerUnread2Id).IsRead);

            // The other user's unread row is untouched.
            Assert.False(rows.Single(n => n.Id == otherUnreadId).IsRead);
        }

        // A subsequent unread query returns nothing for the caller.
        var unreadAfter = await client.GetAsync("/api/v1/notifications?unread=true");
        Assert.Equal(HttpStatusCode.OK, unreadAfter.StatusCode);
        var remaining = await ReadItemsAsync(unreadAfter);
        Assert.Empty(remaining);
    }

    private static async Task<List<NotificationItem>> ReadItemsAsync(HttpResponseMessage response)
    {
        // The notifications endpoint may return either a bare array or an envelope with an "items"
        // array (mirroring the market feed). Support both so the test asserts behavior, not shape.
        var raw = await response.Content.ReadAsStringAsync();
        var trimmed = raw.TrimStart();

        if (trimmed.StartsWith('['))
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<NotificationItem>>(
                raw, JsonOptions) ?? [];
        }

        var envelope = System.Text.Json.JsonSerializer.Deserialize<NotificationEnvelope>(raw, JsonOptions);
        return envelope?.Items ?? [];
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private static Notification NewNotification(
        Guid userId,
        string type,
        string title,
        bool isRead,
        DateTimeOffset at)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = $"body-{title}",
            Data = null,
            IsRead = isRead,
            CreatedAt = at
        };
    }

    private sealed record NotificationEnvelope(
        [property: JsonPropertyName("items")] List<NotificationItem> Items);

    private sealed record NotificationItem(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("body")] string Body,
        [property: JsonPropertyName("data")] string? Data,
        [property: JsonPropertyName("isRead")] bool IsRead,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);
}
