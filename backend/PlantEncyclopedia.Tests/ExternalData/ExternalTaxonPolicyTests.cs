using PlantEncyclopedia.Application.ExternalData;
using PlantEncyclopedia.Infrastructure.ExternalData.Gbif;

namespace PlantEncyclopedia.Tests.ExternalData;

public class ExternalTaxonPolicyTests
{
    private static readonly ExternalTaxon Valid = ExternalTaxa.Species("Rosa", "canina", "4TCZM");

    // Protects against: rejecting the normal case, or storing unnormalized text.
    [Fact]
    public void Evaluate_AcceptsAndNormalizes_ExactAcceptedSpecies()
    {
        var noBreakSpace = ((char)0x00A0).ToString();
        var spaced = Valid with { ScientificName = $"  Rosa{noBreakSpace}  canina " };

        Assert.Null(ExternalTaxonPolicy.Evaluate(spaced, out var normalized));
        Assert.Equal("Rosa canina", normalized.ScientificName);
    }

    [Theory]
    [InlineData(TaxonRank.Subspecies)]
    [InlineData(TaxonRank.Variety)]
    public void Evaluate_AcceptsInfraspecificRanks(TaxonRank rank) =>
        Assert.Null(ExternalTaxonPolicy.Evaluate(Valid with { Rank = rank }, out _));

    // Protects against: the conservative import rules being loosened (ARCHITECTURE §22).
    [Fact]
    public void Evaluate_RejectsFuzzySynonymHigherRankAndNonPlants()
    {
        Assert.Equal(TaxonRejection.NotExactMatch, ExternalTaxonPolicy.Evaluate(Valid with { IsExactMatch = false }, out _));
        Assert.Equal(TaxonRejection.NotAccepted, ExternalTaxonPolicy.Evaluate(Valid with { IsAccepted = false }, out _));
        Assert.Equal(TaxonRejection.RankNotAllowed, ExternalTaxonPolicy.Evaluate(Valid with { Rank = TaxonRank.Other }, out _));
        Assert.Equal(TaxonRejection.NotPlantae, ExternalTaxonPolicy.Evaluate(Plant(Valid, kingdom: "Animalia"), out _));
        Assert.Equal(TaxonRejection.NotPlantae, ExternalTaxonPolicy.Evaluate(Plant(Valid, kingdom: null), out _));
        Assert.Equal(TaxonRejection.NotPlantae, ExternalTaxonPolicy.Evaluate(Plant(Valid, kingdom: "plantae"), out _));
    }

    public static TheoryData<string, ExternalTaxon> InvalidData => new()
    {
        { "one word", Valid with { ScientificName = "Rosa" } },
        { "control character", Valid with { ScientificName = "Rosa\u0000canina x" } },
        { "too long for taxonomy column", Valid with { ScientificName = "Rosa " + new string('a', 96) } },
        { "does not start with genus", Valid with { ScientificName = "Malus canina" } },
        { "missing genus", Valid with { Classification = Valid.Classification with { Genus = null } } },
        { "family too long", Valid with { Classification = Valid.Classification with { Family = new string('a', 101) } } },
        { "external id with slash", Valid with { ExternalId = "../4TCZM" } },
        { "external id with space", Valid with { ExternalId = "4T CZM" } },
        { "external id too long", Valid with { ExternalId = new string('A', 201) } },
        { "blank external id", Valid with { ExternalId = "" } }
    };

    // Protects against: malformed provider data being saved, or failing later on a column limit.
    [Theory]
    [MemberData(nameof(InvalidData))]
    public void Evaluate_RejectsInvalidData(string scenario, ExternalTaxon taxon) =>
        Assert.True(ExternalTaxonPolicy.Evaluate(taxon, out _) == TaxonRejection.InvalidData, scenario);

    [Fact]
    public void Evaluate_AcceptsLongestStorableName() =>
        Assert.Null(ExternalTaxonPolicy.Evaluate(Valid with { ScientificName = "Rosa " + new string('a', 95) }, out _));

    // Protects against: sending blank, oversized or control-character input to the provider.
    [Theory]
    [InlineData("  Rosa   canina ", "Rosa canina")]
    [InlineData("Mentha × piperita", "Mentha × piperita")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("123 456", null)]
    [InlineData("Rosa\tcanina", "Rosa canina")]
    [InlineData("Rosa\u0007canina", null)]
    public void NormalizeRequestedName(string? input, string? expected) =>
        Assert.Equal(expected, ExternalTaxonPolicy.NormalizeRequestedName(input));

    [Fact]
    public void NormalizeRequestedName_RejectsOverlongNames()
    {
        Assert.NotNull(ExternalTaxonPolicy.NormalizeRequestedName(new string('a', 200)));
        Assert.Null(ExternalTaxonPolicy.NormalizeRequestedName(new string('a', 201)));
    }

    private static ExternalTaxon Plant(ExternalTaxon taxon, string? kingdom) =>
        taxon with { Classification = taxon.Classification with { Kingdom = kingdom } };
}

public class GenusPlantGroupMapTests
{
    // Protects against: a genus matching a group by accident (case-insensitive or partial matches).
    [Fact]
    public void FindSlug_IsExactAndCaseSensitive()
    {
        var map = new GenusPlantGroupMap(new Dictionary<string, string> { ["Rosa"] = "roses" });

        Assert.Equal("roses", map.FindSlug("Rosa"));
        Assert.Null(map.FindSlug("rosa"));
        Assert.Null(map.FindSlug("Ros"));
        Assert.Null(map.FindSlug("Solanum"));
        Assert.Null(map.FindSlug(null));
    }

    [Fact]
    public void MissingConfiguration_MapsNothing() => Assert.Equal(0, new GenusPlantGroupMap(null).Count);

    // Protects against: a typo in configuration silently disabling or misdirecting publication.
    [Theory]
    [InlineData("rosa", "roses")]
    [InlineData("Rosa canina", "roses")]
    [InlineData("Rosa", "Roses")]
    [InlineData("Rosa", "roses!")]
    [InlineData("Rosa", "")]
    public void InvalidEntries_Throw(string genus, string slug) =>
        Assert.Throws<ArgumentException>(() => new GenusPlantGroupMap(new Dictionary<string, string> { [genus] = slug }));
}

public class GbifOptionsTests
{
    [Fact]
    public void Disabled_NeedsNoSettings() => new GbifOptions().Validate();

    [Fact]
    public void Enabled_AcceptsCompleteSettings() => GbifFixtures.Options().Validate();

    // Protects against: silently matching against the frozen Backbone, or sending an unusable contact.
    [Theory]
    [InlineData(null, GbifFixtures.Contact)]
    [InlineData("col-xr", GbifFixtures.Contact)]
    [InlineData(GbifFixtures.ChecklistKey, null)]
    [InlineData(GbifFixtures.ChecklistKey, "github.com/lkim07/Plant-Encyclopedia")]
    [InlineData(GbifFixtures.ChecklistKey, "http://github.com/lkim07/Plant-Encyclopedia")]
    [InlineData(GbifFixtures.ChecklistKey, "https://user:secret@example.org/")]
    public void Enabled_RejectsIncompleteSettings(string? checklistKey, string? contact) =>
        Assert.Throws<InvalidOperationException>(() =>
            new GbifOptions { Enabled = true, ChecklistKey = checklistKey, Contact = contact }.Validate());
}
