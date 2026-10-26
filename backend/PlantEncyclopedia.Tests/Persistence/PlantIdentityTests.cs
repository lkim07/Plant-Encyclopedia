using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantEncyclopedia.Domain.Plants;

namespace PlantEncyclopedia.Tests.Persistence;

/// <summary>
/// Plant identity (migration AddPlantIdentityKey): one plant per normalized scientific name +
/// normalized cultivar name. Each test uses its own random genus, so tests never collide with each other.
/// Placeholders: {g} genus, {G} genus in capitals, {nbsp} no-break space, {ideo} ideographic space,
/// {diaeresis} combining diaeresis (U+0308).
/// </summary>
[Collection(PostgresCollection.Name)]
public class PlantIdentityTests(PostgresDatabaseFixture fixture)
{
    private const string IdentityIndex = "ux_plants_identity";

    // Protects against: the normalization rule drifting from the reviewed definition.
    [Theory]
    [InlineData("Rosa × hybrida", "rosa × hybrida")]
    [InlineData("rosa x hybrida", "rosa × hybrida")]
    [InlineData("Rosa ×hybrida", "rosa × hybrida")]
    [InlineData("  ROSA   X  HYBRIDA ", "rosa × hybrida")]
    [InlineData("Rosa{nbsp}×{ideo}hybrida", "rosa × hybrida")]
    [InlineData("×Chitalpa tashkentensis", "× chitalpa tashkentensis")]
    [InlineData("Rosa hybrida", "rosa hybrida")]
    [InlineData("Pyrus xanthocarpa", "pyrus xanthocarpa")]
    [InlineData("Cephaëlis ipecacuanha", "cephaelis ipecacuanha")]
    [InlineData("Cephae{diaeresis}lis ipecacuanha", "cephaelis ipecacuanha")]
    [InlineData("Capsella bursa-pastoris", "capsella bursa-pastoris")]
    [InlineData("Rosa canina subsp. dumalis", "rosa canina subsp. dumalis")]
    public async Task ScientificIdentity_Normalizes(string input, string expected)
    {
        Assert.Equal(expected, await ScalarAsync("plant_scientific_identity", Expand(input, "")));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Peace", "peace")]
    [InlineData("'Peace'", "peace")]
    [InlineData("‘Peace’", "peace")]
    [InlineData("“ PEACE ”", "peace")]
    [InlineData("Gloire{nbsp} de  Dijon", "gloire de dijon")]
    [InlineData("Mr. Lincoln", "mr. lincoln")]
    [InlineData("Ophélia", "ophélia")]
    [InlineData("Beni-kaname", "beni-kaname")]
    public async Task CultivarIdentity_Normalizes(string? input, string expected)
    {
        Assert.Equal(
            expected,
            await ScalarAsync("plant_cultivar_identity", ExpandOptional(input, "")));
    }

    // Protects against: the same plant being stored twice under trivially different spellings.
    [Theory]
    [InlineData("{g} vulgaris", null, "{g} vulgaris", null)]
    [InlineData("{g} vulgaris", null, "{G}   VULGARIS ", null)]
    [InlineData("{g} vulgaris", null, "{g}{nbsp}vulgaris", null)]
    [InlineData("{g} × hybrida", null, "{g} x hybrida", null)]
    [InlineData("{g} × hybrida", null, "{g} ×hybrida", null)]
    [InlineData("{g} ipecacuanhaë", null, "{g} ipecacuanhae", null)]
    [InlineData("{g} vulgaris", null, "{g} vulgaris", "")]
    [InlineData("{g} vulgaris", "", "{g} vulgaris", "   ")]
    [InlineData("{g} vulgaris", "Peace", "{g} vulgaris", "'PEACE'")]
    [InlineData("{g} vulgaris", "Gloire de Dijon", "{g} vulgaris", "‘Gloire  de Dijon’")]
    public async Task Duplicates_AreRejected(string firstScientific, string? firstCultivar, string secondScientific, string? secondCultivar)
    {
        var genus = NewGenus();
        await InsertAsync(Expand(firstScientific, genus), ExpandOptional(firstCultivar, genus));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertAsync(Expand(secondScientific, genus), ExpandOptional(secondCultivar, genus)));

        AssertIdentityViolation(error);
    }

    // Protects against: the rule being too aggressive and blocking genuinely different plants.
    [Theory]
    [InlineData("{g} vulgaris", "Peace", "{g} vulgaris", "Iceberg")]
    [InlineData("{g} vulgaris", null, "{g} vulgaris", "Peace")]
    [InlineData("{g} × hybrida", null, "{g} hybrida", null)]
    [InlineData("{g} bursa-pastoris", null, "{g} bursa pastoris", null)]
    [InlineData("{g} canina subsp. dumalis", null, "{g} canina var. dumalis", null)]
    [InlineData("{g} xanthocarpa", null, "{g} × anthocarpa", null)]
    [InlineData("{g} vulgaris", "Mr. Lincoln", "{g} vulgaris", "Mr Lincoln")]
    [InlineData("{g} vulgaris", "Ophélia", "{g} vulgaris", "Ophelia")]
    public async Task DistinctPlants_AreAllowed(string firstScientific, string? firstCultivar, string secondScientific, string? secondCultivar)
    {
        var genus = NewGenus();

        await InsertAsync(Expand(firstScientific, genus), ExpandOptional(firstCultivar, genus));
        await InsertAsync(Expand(secondScientific, genus), ExpandOptional(secondCultivar, genus));
    }

    // Protects against: hidden (Draft) records duplicating a Published plant.
    [Fact]
    public async Task Duplicates_AreRejectedWhateverTheStatus()
    {
        var genus = NewGenus();
        await InsertAsync($"{genus} vulgaris", null, PlantStatus.Archived);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertAsync($"{genus} vulgaris", null, PlantStatus.Draft));

        AssertIdentityViolation(error);
    }

    // Protects against: two simultaneous imports of the same plant both succeeding (DATABASE_DESIGN §59).
    [Fact]
    public async Task ConcurrentInserts_SecondWaitsThenFails()
    {
        var scientificName = $"{NewGenus()} vulgaris";

        await using var first = fixture.CreateDbContext();
        await using var second = fixture.CreateDbContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        first.Plants.Add(NewPlant(scientificName, null));
        await first.SaveChangesAsync(); // inserted but not committed

        second.Plants.Add(NewPlant(scientificName, null));
        var secondSave = second.SaveChangesAsync();

        // The second insert must wait for the first transaction rather than succeed or fail early.
        await Task.Delay(500);
        Assert.False(secondSave.IsCompleted);

        await firstTransaction.CommitAsync();

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => secondSave);
        AssertIdentityViolation(error);
    }

    private static void AssertIdentityViolation(DbUpdateException error)
    {
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(IdentityIndex, postgres.ConstraintName);
    }

    private async Task InsertAsync(string scientificName, string? cultivarName, PlantStatus status = PlantStatus.Draft)
    {
        await using var db = fixture.CreateDbContext();
        db.Plants.Add(NewPlant(scientificName, cultivarName, status));
        await db.SaveChangesAsync();
    }

    private static Plant NewPlant(string scientificName, string? cultivarName, PlantStatus status = PlantStatus.Draft) => new()
    {
        DisplayName = $"Test plant {Guid.NewGuid():N}",
        ScientificName = scientificName,
        CultivarName = cultivarName,
        Status = status // without a group
    };

    private async Task<string?> ScalarAsync(string function, string? input)
    {
        await using var db = fixture.CreateDbContext();
        var query = function switch
        {
            "plant_scientific_identity" => db.Database.SqlQuery<string>($"SELECT public.plant_scientific_identity({input}) AS \"Value\""),
            "plant_cultivar_identity" => db.Database.SqlQuery<string>($"SELECT public.plant_cultivar_identity({input}) AS \"Value\""),
            _ => throw new ArgumentOutOfRangeException(nameof(function))
        };
        return await query.SingleAsync();
    }

    /// <summary>A random genus of letters only (no digits, no standalone "x"), e.g. "Testgenusqwjk…".</summary>
    private static string NewGenus() =>
        "Testgenus" + string.Concat(Guid.NewGuid().ToString("N").Select(c => (char)('k' + (c % 10))));

    private static string Expand(string text, string genus) => text
        .Replace("{g}", genus)
        .Replace("{G}", genus.ToUpperInvariant())
        .Replace("{nbsp}", ((char)0x00A0).ToString())
        .Replace("{ideo}", ((char)0x3000).ToString())
        .Replace("{diaeresis}", ((char)0x0308).ToString());

    private static string? ExpandOptional(string? text, string genus) =>
        text is null ? null : Expand(text, genus);
}
