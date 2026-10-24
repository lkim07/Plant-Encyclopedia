using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PlantEncyclopedia.Domain.Favorites;
using PlantEncyclopedia.Domain.Plants;
using PlantEncyclopedia.Infrastructure.Identity;
using PlantEncyclopedia.Tests.Persistence;

namespace PlantEncyclopedia.Tests.Api;

/// <summary>
/// Favorites (API_SPEC §31–34): saving is repeat-safe (a bookmark toggle), every operation is limited to the
/// signed-in user's own favorites, and the order stays 1…n.
/// </summary>
[Collection(PostgresCollection.Name)]
public class FavoriteEndpointTests(PostgresDatabaseFixture fixture)
{
    // Protects against: favorites readable or changeable without signing in.
    [Fact]
    public async Task Favorites_RequireSignIn()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var browser = await TestBrowser.OpenAsync(factory);

        var list = await browser.SendAsync(HttpMethod.Get, "/api/favorites");
        var save = await browser.PostAsync("/api/favorites", new { plantId = Guid.NewGuid() });

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (list.StatusCode, save.StatusCode));
        Assert.Contains("UNAUTHENTICATED", await list.Content.ReadAsStringAsync());
    }

    // Protects against: saving before the email address is confirmed (D-041).
    [Fact]
    public async Task Save_NeedsAConfirmedEmail()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory, confirmed: false);
        var plant = await CreatePlantAsync("Rose");

        var response = await browser.PostAsync("/api/favorites", new { plantId = plant.Id });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("EMAIL_NOT_CONFIRMED", await response.Content.ReadAsStringAsync());
    }

    // Protects against: a double click creating two favorites or an error (the bookmark is a toggle).
    [Fact]
    public async Task Save_IsRepeatSafe_AndReturnsTheCard()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var plant = await CreatePlantAsync("Dog rose", image: "/media/dog-rose-medium.webp");

        var first = await browser.PostAsync("/api/favorites", new { plantId = plant.Id });
        var again = await browser.PostAsync("/api/favorites", new { plantId = plant.Id });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.StatusCode, again.StatusCode));
        var saved = await DataAsync(first);
        Assert.Equal(saved.GetProperty("favoriteId").GetGuid(), (await DataAsync(again)).GetProperty("favoriteId").GetGuid());
        Assert.Equal(1, saved.GetProperty("order").GetInt32());
        Assert.Equal(
            $$"""{"id":"{{plant.Id}}","name":"Dog rose","plantGroup":"Roses","imageUrl":"/media/dog-rose-medium.webp"}""",
            saved.GetProperty("plant").GetRawText());
        Assert.Single((await ListAsync(browser)).EnumerateArray());
    }

    [Fact]
    public async Task Save_RefusesPlantsThatAreNotPublished()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var draft = await CreatePlantAsync("Draft", PlantStatus.Draft);

        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/favorites", new { plantId = draft.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.PostAsync("/api/favorites", new { plantId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/api/favorites", new { })).StatusCode);
    }

    // Protects against: gaps or duplicates in the order after removing, and Undo putting the plant somewhere else.
    [Fact]
    public async Task Remove_ClosesTheGap_AndUndoRestoresThePosition()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var (a, b, c) = (await CreatePlantAsync("A"), await CreatePlantAsync("B"), await CreatePlantAsync("C"));
        foreach (var plant in new[] { a, b, c }) await browser.PostAsync("/api/favorites", new { plantId = plant.Id });
        var bFavorite = await FavoriteIdAsync(browser, b.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await browser.SendAsync(HttpMethod.Delete, $"/api/favorites/{bFavorite}")).StatusCode);
        Assert.Equal([("A", 1), ("C", 2)], await OrderAsync(browser));

        await browser.PostAsync("/api/favorites", new { plantId = b.Id, order = 2 });
        Assert.Equal([("A", 1), ("B", 2), ("C", 3)], await OrderAsync(browser));
    }

    // Protects against: a half-applied or inconsistent reorder.
    [Fact]
    public async Task Reorder_SetsTheWholeOrder_OrNothing()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var (a, b, c) = (await CreatePlantAsync("A"), await CreatePlantAsync("B"), await CreatePlantAsync("C"));
        foreach (var plant in new[] { a, b, c }) await browser.PostAsync("/api/favorites", new { plantId = plant.Id });
        var (fa, fb, fc) = (await FavoriteIdAsync(browser, a.Id), await FavoriteIdAsync(browser, b.Id), await FavoriteIdAsync(browser, c.Id));

        var reordered = await browser.SendAsync(HttpMethod.Patch, "/api/favorites/order", new
        {
            items = new[] { new { favoriteId = fc, order = 1 }, new { favoriteId = fa, order = 2 }, new { favoriteId = fb, order = 3 } }
        });
        Assert.Equal(HttpStatusCode.NoContent, reordered.StatusCode);
        Assert.Equal([("C", 1), ("A", 2), ("B", 3)], await OrderAsync(browser));

        var missing = await browser.SendAsync(HttpMethod.Patch, "/api/favorites/order", new
        {
            items = new[] { new { favoriteId = fa, order = 1 }, new { favoriteId = fb, order = 2 } }
        });
        var duplicateOrder = await browser.SendAsync(HttpMethod.Patch, "/api/favorites/order", new
        {
            items = new[] { new { favoriteId = fa, order = 1 }, new { favoriteId = fb, order = 1 }, new { favoriteId = fc, order = 2 } }
        });
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (missing.StatusCode, duplicateOrder.StatusCode));
        Assert.Equal([("C", 1), ("A", 2), ("B", 3)], await OrderAsync(browser));
    }

    // Protects against: one user seeing, removing or reordering another user's favorites by changing an ID (API_SPEC §7).
    [Fact]
    public async Task Users_CannotTouchEachOthersFavorites()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (alice, _) = await SignInAsync(factory);
        var (mallory, _) = await SignInAsync(factory);
        var plant = await CreatePlantAsync("Rose");
        await alice.PostAsync("/api/favorites", new { plantId = plant.Id });
        var aliceFavorite = await FavoriteIdAsync(alice, plant.Id);

        Assert.Empty((await ListAsync(mallory)).EnumerateArray());
        var remove = await mallory.SendAsync(HttpMethod.Delete, $"/api/favorites/{aliceFavorite}");
        var reorder = await mallory.SendAsync(HttpMethod.Patch, "/api/favorites/order", new
        {
            items = new[] { new { favoriteId = aliceFavorite, order = 1 } }
        });

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.BadRequest), (remove.StatusCode, reorder.StatusCode));
        Assert.Single((await ListAsync(alice)).EnumerateArray());
    }

    // Protects against: another site saving or removing favorites through a signed-in browser (CSRF).
    [Fact]
    public async Task Changes_NeedTheCsrfToken()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var plant = await CreatePlantAsync("Rose");

        var response = await browser.Client.PostAsync("/api/favorites",
            new StringContent($$"""{"plantId":"{{plant.Id}}"}""", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("CSRF_TOKEN_INVALID", await response.Content.ReadAsStringAsync());
    }

    // Protects against: simultaneous saves (two tabs, quick clicks) giving duplicate rows or duplicate positions.
    [Fact]
    public async Task SimultaneousSaves_KeepOneRowPerPlant_AndDistinctPositions()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, userId) = await SignInAsync(factory);
        var plants = new List<Plant>();
        for (var i = 0; i < 8; i++) plants.Add(await CreatePlantAsync($"P{i}"));

        var saves = plants.Select(p => browser.PostAsync("/api/favorites", new { plantId = p.Id }))
            .Concat(plants.Take(3).Select(p => browser.PostAsync("/api/favorites", new { plantId = p.Id })));
        var responses = await Task.WhenAll(saves);

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, r.StatusCode.ToString()));
        await using var db = fixture.CreateDbContext();
        var positions = await db.Favorites.Where(f => f.UserId == userId).Select(f => f.Position).OrderBy(p => p).ToListAsync();
        Assert.Equal(Enumerable.Range(1, 8), positions);
    }

    // Protects against: one account filling the database with favorites.
    [Fact]
    public async Task Save_StopsAtTheLimit()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, userId) = await SignInAsync(factory);
        var plant = await CreatePlantAsync("Rose");
        await using (var db = fixture.CreateDbContext())
        {
            var group = TestData.NewPlantGroup(TestData.NewCategory());
            for (var i = 1; i <= Application.Favorites.IFavoriteStore.MaxFavorites; i++)
            {
                var filler = TestData.NewPlant(group, $"Filler {i}");
                db.Plants.Add(filler);
                db.Favorites.Add(new Favorite { UserId = userId, Plant = filler, Position = i });
            }

            await db.SaveChangesAsync();
        }

        var response = await browser.PostAsync("/api/favorites", new { plantId = plant.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FAVORITES_LIMIT", await response.Content.ReadAsStringAsync());
    }

    // Protects against: favorites of a plant that stops being published showing (or breaking the order).
    [Fact]
    public async Task UnpublishedPlants_AreHidden_AndTheOrderSkipsThem()
    {
        await using var factory = new TestApiFactory(fixture.ConnectionString);
        var (browser, _) = await SignInAsync(factory);
        var (a, b, c) = (await CreatePlantAsync("A"), await CreatePlantAsync("B"), await CreatePlantAsync("C"));
        foreach (var plant in new[] { a, b, c }) await browser.PostAsync("/api/favorites", new { plantId = plant.Id });

        await using (var db = fixture.CreateDbContext())
        {
            await db.Plants.Where(p => p.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PlantStatus.Archived));
        }

        Assert.Equal([("A", 1), ("C", 2)], await OrderAsync(browser));
    }

    private async Task<(TestBrowser Browser, Guid UserId)> SignInAsync(TestApiFactory factory, bool confirmed = true)
    {
        var browser = await TestBrowser.OpenAsync(factory);
        var email = $"user-{Guid.NewGuid():N}@example.org";
        var created = await browser.PostAsync("/api/auth/register", new { email, password = "plant lover", displayName = "Tester" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        if (confirmed)
        {
            user.EmailConfirmed = true;
            await users.UpdateAsync(user);
        }

        return (browser, user.Id);
    }

    private async Task<Plant> CreatePlantAsync(string name, PlantStatus status = PlantStatus.Published, string? image = null)
    {
        await using var db = fixture.CreateDbContext();
        var plant = TestData.NewPlant(TestData.NewPlantGroup(TestData.NewCategory(), "Roses"), name, status);
        if (image is not null)
        {
            plant.Images.Add(new PlantImage { Url = image, MediumUrl = image, Source = "test", RetrievedAt = DateTimeOffset.UtcNow });
        }

        db.Plants.Add(plant);
        await db.SaveChangesAsync();
        return plant;
    }

    private static async Task<JsonElement> ListAsync(TestBrowser browser) =>
        await DataAsync(await browser.SendAsync(HttpMethod.Get, "/api/favorites"));

    private static async Task<Guid> FavoriteIdAsync(TestBrowser browser, Guid plantId) =>
        (await ListAsync(browser)).EnumerateArray()
            .Single(f => f.GetProperty("plant").GetProperty("id").GetGuid() == plantId)
            .GetProperty("favoriteId").GetGuid();

    private static async Task<List<(string, int)>> OrderAsync(TestBrowser browser) =>
        (await ListAsync(browser)).EnumerateArray()
            .Select(f => (f.GetProperty("plant").GetProperty("name").GetString()!, f.GetProperty("order").GetInt32()))
            .ToList();

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
