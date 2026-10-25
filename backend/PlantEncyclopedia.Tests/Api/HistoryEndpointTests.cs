using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Application.History;
using PlantEncyclopedia.Domain.History;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Search and view history (API_SPEC §28–29): signed-in users only, each user's own entries only, repeats folded
/// together, and only the newest entries kept.
/// </summary>
[Collection(PostgresCollection.Name)]
public class HistoryEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: history being read or recorded for visitors who are not signed in (they have none on the server).
    [Fact]
    public async Task History_RequiresSignIn()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var list = await browser.SendAsync(HttpMethod.Get, "/api/history/searches");
        var search = await browser.PostAsync("/api/history/searches", new { query = "rose" });
        var view = await browser.PostAsync("/api/history/views", new { plantId = Guid.NewGuid() });

        Assert.Equal(
            (HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized),
            (list.StatusCode, search.StatusCode, view.StatusCode));
    }

    // Protects against: duplicate recent searches, wrong order, or unnormalized text (UX §20).
    [Fact]
    public async Task Searches_AreNewestFirst_AndARepeatMovesToTheTop()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);

        var first = await browser.PostAsync("/api/history/searches", new { query = "  ｒｏｓｅ " });
        await browser.PostAsync("/api/history/searches", new { query = "peony" });
        var again = await browser.PostAsync("/api/history/searches", new { query = "Rose" });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, again.StatusCode));
        Assert.Equal("rose", (await DataAsync(first)).GetProperty("query").GetString());
        Assert.Equal(
            (await DataAsync(first)).GetProperty("searchId").GetGuid(),
            (await DataAsync(again)).GetProperty("searchId").GetGuid());
        Assert.Equal(["Rose", "peony"], await QueriesAsync(browser));
    }

    [Fact]
    public async Task Searches_ValidateTheText()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);

        var empty = await browser.PostAsync("/api/history/searches", new { query = "  " });
        var tooLong = await browser.PostAsync("/api/history/searches", new { query = new string('a', 101) });

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (empty.StatusCode, tooLong.StatusCode));
        Assert.Contains("\"query\"", await empty.Content.ReadAsStringAsync());
        Assert.Empty(await QueriesAsync(browser));
    }

    // Protects against: one user reading or removing another user's searches by changing an ID (API_SPEC §7).
    [Fact]
    public async Task Remove_DeletesOnlyTheUsersOwnSearch()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (alice, _) = await SignInAsync(factory);
        var (mallory, _) = await SignInAsync(factory);
        var rose = (await DataAsync(await alice.PostAsync("/api/history/searches", new { query = "rose" }))).GetProperty("searchId").GetGuid();
        var peony = (await DataAsync(await alice.PostAsync("/api/history/searches", new { query = "peony" }))).GetProperty("searchId").GetGuid();

        Assert.Empty(await QueriesAsync(mallory));
        Assert.Equal(HttpStatusCode.NotFound, (await mallory.SendAsync(HttpMethod.Delete, $"/api/history/searches/{rose}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.SendAsync(HttpMethod.Delete, $"/api/history/searches/{peony}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.SendAsync(HttpMethod.Delete, $"/api/history/searches/{peony}")).StatusCode);
        Assert.Equal(["rose"], await QueriesAsync(alice));
    }

    // Protects against: search history growing without bound, or more than the recent ones being returned.
    [Fact]
    public async Task Searches_KeepOnlyTheNewest()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, userId) = await SignInAsync(factory);
        await using (var db = fixture.CreateDbContext())
        {
            var start = DateTimeOffset.UtcNow.AddDays(-1);
            for (var i = 1; i <= IHistoryStore.MaxSearches; i++)
            {
                db.SearchHistory.Add(new SearchHistoryEntry { UserId = userId, Query = $"old {i}", QueryKey = $"old {i}", SearchedAt = start.AddMinutes(i) });
            }

            await db.SaveChangesAsync();
        }

        await browser.PostAsync("/api/history/searches", new { query = "rose" });

        await using (var db = fixture.CreateDbContext())
        {
            var kept = await db.SearchHistory.Where(s => s.UserId == userId).Select(s => s.Query).ToListAsync();
            Assert.Equal(IHistoryStore.MaxSearches, kept.Count);
            Assert.DoesNotContain("old 1", kept);
        }

        var recent = await QueriesAsync(browser);
        Assert.Equal(IHistoryStore.RecentSearchLimit, recent.Count);
        Assert.Equal(["rose", "old 50", "old 49"], recent.Take(3));
    }

    // Protects against: reloads and Back-and-forth counting as many views, or views of hidden plants being recorded.
    [Fact]
    public async Task Views_AreRecorded_ButNotTwiceInAShortTime()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, userId) = await SignInAsync(factory);
        var rose = await CreatePlantAsync("Rose");
        var draft = await CreatePlantAsync("Draft", PlantStatus.Draft);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/history/views", new { plantId = rose.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await browser.PostAsync("/api/history/views", new { plantId = rose.Id })).StatusCode);
        Assert.Equal(1, await ViewCountAsync(userId));

        await using (var db = fixture.CreateDbContext())
        {
            var earlier = DateTimeOffset.UtcNow.AddMinutes(-(IHistoryStore.ViewRepeatMinutes + 1));
            await db.ViewHistory.Where(v => v.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(v => v.ViewedAt, earlier));
        }

        await browser.PostAsync("/api/history/views", new { plantId = rose.Id });
        Assert.Equal(2, await ViewCountAsync(userId));

        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/history/views", new { plantId = draft.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/history/views", new { plantId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/api/history/views", new { })).StatusCode);
        Assert.Equal(2, await ViewCountAsync(userId));
    }

    // Protects against: view history growing without bound.
    [Fact]
    public async Task Views_KeepOnlyTheNewest()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, userId) = await SignInAsync(factory);
        var rose = await CreatePlantAsync("Rose");
        var oldest = Guid.Empty;
        await using (var db = fixture.CreateDbContext())
        {
            var start = DateTimeOffset.UtcNow.AddDays(-1);
            for (var i = 1; i <= IHistoryStore.MaxViews; i++)
            {
                var entry = new ViewHistoryEntry { Id = Guid.NewGuid(), UserId = userId, PlantId = rose.Id, ViewedAt = start.AddMinutes(i) };
                if (i == 1) oldest = entry.Id;
                db.ViewHistory.Add(entry);
            }

            await db.SaveChangesAsync();
        }

        await browser.PostAsync("/api/history/views", new { plantId = rose.Id });

        Assert.Equal(IHistoryStore.MaxViews, await ViewCountAsync(userId));
        await using var check = fixture.CreateDbContext();
        Assert.False(await check.ViewHistory.AnyAsync(v => v.Id == oldest));
    }

    // Protects against: another site recording or removing history through a signed-in browser (CSRF).
    [Fact]
    public async Task Changes_NeedTheCsrfToken()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);

        var response = await browser.Client.PostAsync("/api/history/searches",
            new StringContent("""{"query":"rose"}""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await QueriesAsync(browser));
    }

    // History does not need a confirmed email: it is a private signal, unlike Favorites (D-041).
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

    private async Task<Plant> CreatePlantAsync(string name, PlantStatus status = PlantStatus.Published)
    {
        await using var db = fixture.CreateDbContext();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory()), name, status);
        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    private async Task<int> ViewCountAsync(Guid userId)
    {
        await using var db = fixture.CreateDbContext();
        return await db.ViewHistory.CountAsync(v => v.UserId == userId);
    }

    private static async Task<List<string>> QueriesAsync(TestBrowser browser) =>
        (await DataAsync(await browser.SendAsync(HttpMethod.Get, "/api/history/searches"))).EnumerateArray()
            .Select(s => s.GetProperty("query").GetString()!)
            .ToList();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
