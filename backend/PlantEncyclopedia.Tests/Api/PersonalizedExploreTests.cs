using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// GET /api/explore for signed-in users (API_SPEC §20–22): personalized from their own history, stable across pages
/// with feedAt, and a removed search stops counting. The test database holds plants from other tests too, so the
/// checks look at this test's groups inside the whole feed.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PersonalizedExploreTests(PostgresDatabaseFixture fixture)
{
    // Protects against: signed-in users getting a feed that ignores what they look at (PRD §10).
    [Fact]
    public async Task Feed_StartsWithTheGroupTheUserViews_AndStaysDiverse()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var ferns = await CreateGroupAsync(plants: 4);
        foreach (var plant in ferns.Take(3)) await browser.PostAsync("/api/history/views", new { plantId = plant.Id });

        var (cards, _) = await PageAsync(browser, page: 1, pageSize: 10);

        Assert.Equal(ferns[3].Id, cards[0]); // the strongest group's unseen plant comes first
        Assert.InRange(cards.Count(id => ferns.Any(p => p.Id == id)), 1, 6); // not a feed of ferns only
    }

    // Protects against: infinite scroll repeating or skipping plants when the user's activity changes mid-scroll.
    [Fact]
    public async Task FeedAt_KeepsTheOrderOfOneFeed_WhileANewFeedUsesNewActivity()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var (ferns, mosses) = (await CreateGroupAsync(plants: 3), await CreateGroupAsync(plants: 5));
        foreach (var plant in ferns) await browser.PostAsync("/api/history/views", new { plantId = plant.Id });
        var (_, feedAt) = await PageAsync(browser, page: 1, pageSize: 5);
        var before = await WholeFeedAsync(browser, feedAt, pageSize: 50);

        foreach (var plant in mosses.Take(4)) await browser.PostAsync("/api/history/views", new { plantId = plant.Id });

        Assert.Equal(before, await WholeFeedAsync(browser, feedAt, pageSize: 7));
        var (fresh, _) = await PageAsync(browser, page: 1, pageSize: 5);
        Assert.Equal(mosses[4].Id, fresh[0]);
    }

    // Protects against: a removed search still steering the feed (PRD §16, UX §20).
    [Fact]
    public async Task RemovingASearch_RemovesItsInfluence()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        using var anonymous = factory.CreateClient();
        var groupName = $"Zq{Guid.NewGuid():N}";
        var searched = await CreateGroupAsync(plants: 2, groupName);
        var viewed = await CreateGroupAsync(plants: 1);
        await browser.PostAsync("/api/history/views", new { plantId = viewed[0].Id });
        var search = await browser.PostAsync("/api/history/searches", new { query = groupName });
        var searchId = (await DataAsync(search)).GetProperty("searchId").GetGuid();

        var (personalized, _) = await PageAsync(browser, page: 1, pageSize: 10);
        Assert.Contains(personalized[0], searched.Select(p => p.Id));

        await browser.SendAsync(HttpMethod.Delete, $"/api/history/searches/{searchId}");

        var (after, _) = await PageAsync(browser, page: 1, pageSize: 10);
        Assert.Equal(await AnonymousPageAsync(anonymous), after);
    }

    // Protects against: one user's activity shaping another user's feed.
    [Fact]
    public async Task AnotherUsersActivity_DoesNotCount()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (alice, _) = await SignInAsync(factory);
        var (bob, _) = await SignInAsync(factory);
        using var anonymous = factory.CreateClient();
        foreach (var plant in await CreateGroupAsync(plants: 3)) await alice.PostAsync("/api/history/views", new { plantId = plant.Id });

        var (bobs, _) = await PageAsync(bob, page: 1, pageSize: 10);

        Assert.Equal(await AnonymousPageAsync(anonymous), bobs);
    }

    [Fact]
    public async Task FeedAt_IsValidated_AndAlwaysReturned()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        var invalid = await client.GetAsync("/api/explore?feedAt=yesterday");
        var valid = await client.GetAsync("/api/explore?feedAt=2020-01-02T03%3A04%3A05Z");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("\"feedAt\"", await invalid.Content.ReadAsStringAsync());
        using var json = JsonDocument.Parse(await valid.Content.ReadAsStringAsync());
        Assert.Equal("2020-01-02T03:04:05.0000000Z", json.RootElement.GetProperty("feedAt").GetString());
    }

    private static async Task<(List<Guid> Cards, string FeedAt)> PageAsync(TestBrowser browser, int page, int pageSize, string? feedAt = null)
    {
        var query = $"/api/explore?page={page}&pageSize={pageSize}" + (feedAt is null ? "" : $"&feedAt={Uri.EscapeDataString(feedAt)}");
        var response = await browser.SendAsync(HttpMethod.Get, query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            json.RootElement.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList(),
            json.RootElement.GetProperty("feedAt").GetString()!);
    }

    private static async Task<List<Guid>> WholeFeedAsync(TestBrowser browser, string feedAt, int pageSize)
    {
        var all = new List<Guid>();
        for (var page = 1; ; page++)
        {
            var (cards, _) = await PageAsync(browser, page, pageSize, feedAt);
            all.AddRange(cards);
            if (cards.Count < pageSize) return all;
        }
    }

    private static async Task<List<Guid>> AnonymousPageAsync(HttpClient client)
    {
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/explore?page=1&pageSize=10"));
        return json.RootElement.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToList();
    }

    private async Task<List<Plant>> CreateGroupAsync(int plants, string groupName = "Test Group")
    {
        await using var db = fixture.CreateDbContext();
        var group = TestData.NewPlantGroup(TestData.NewCategory(), groupName);
        var created = Enumerable.Range(1, plants).Select(i => TestData.NewPlant(group, $"Plant {i}")).ToList();
        db.Plants.AddRange(created);
        await db.SaveChangesAsync();
        return created;
    }

    private static async Task<(TestBrowser Browser, Guid UserId)> SignInAsync(TestApiFactory factory)
    {
        var browser = await TestBrowser.OpenAsync(factory);
        var email = $"user-{Guid.NewGuid():N}@example.org";
        var created = await browser.PostAsync("/api/auth/register", new { email, password = "plant lover", displayName = "Tester" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return (browser, (await users.FindByEmailAsync(email))!.Id);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
