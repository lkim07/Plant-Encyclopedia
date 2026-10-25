namespace PlantEncyclopedia.Application.Explore;

/// <summary>A Published plant that can appear in the feed.</summary>
/// <param name="Seen">The user has viewed or saved it; it comes after unseen plants of its group.</param>
public sealed record FeedPlant(Guid Id, Guid GroupId, Guid CategoryId, string Name, bool HasImage, bool Seen);

/// <summary>One piece of interest in a plant group: a view, a saved plant, or (a share of) a search.</summary>
public sealed record FeedSignal(Guid GroupId, double Weight, DateTimeOffset At);

/// <summary>
/// The rule-based personalized Explore order (PRD §10–12, D-020–D-023, ARCHITECTURE §31–32). All weights are here,
/// so they are easy to tune.
/// <list type="number">
/// <item>Interest: each plant group scores the weights of its signals, halved every <see cref="HalfLifeDays"/>
/// days; a category scores the sum of its groups.</item>
/// <item>Each plant falls in one bucket: strongly related (its group has interest), adjacent (another group of a
/// category with interest) or novel (a category without interest).</item>
/// <item>Inside a bucket, groups take turns (every group's first plant, then every group's second, …), the more
/// interesting groups or categories first in each round. Inside a group, unseen plants come first, then plants with
/// an image, then by name.</item>
/// <item>The buckets are interleaved in the repeating pattern <see cref="Pattern"/> (6 strong, 2 adjacent,
/// 2 novel in every 10); an empty bucket's turn goes to the next one.</item>
/// </list>
/// Each plant appears once. Nothing is random, so the same signals give the same order.
/// </summary>
public static class PersonalizedFeed
{
    public const double ViewWeight = 1;
    public const double FavoriteWeight = 3;

    /// <summary>Shared among the plant groups the search text matches.</summary>
    public const double SearchWeight = 2;

    public const double HalfLifeDays = 14;

    /// <summary>Total signal weight (before ageing) needed to personalize; below it the default feed is used.</summary>
    public const double MinInterest = 3;

    /// <summary>S strongly related, A adjacent, N novel: the 60/20/20 guideline (D-020) in every 10 cards.</summary>
    public const string Pattern = "SSASSNSSAN";

    /// <summary>The plant IDs in feed order, or null when there is too little interest to personalize.</summary>
    public static IReadOnlyList<Guid>? Order(IReadOnlyList<FeedPlant> plants, IReadOnlyList<FeedSignal> signals, DateTimeOffset asOf)
    {
        if (signals.Sum(s => s.Weight) < MinInterest)
        {
            return null;
        }

        var groupScores = new Dictionary<Guid, double>();
        foreach (var signal in signals)
        {
            var ageDays = Math.Max(0, (asOf - signal.At).TotalDays);
            groupScores[signal.GroupId] = groupScores.GetValueOrDefault(signal.GroupId) + signal.Weight * Math.Pow(0.5, ageDays / HalfLifeDays);
        }

        var categoryScores = plants
            .DistinctBy(p => p.GroupId)
            .GroupBy(p => p.CategoryId)
            .ToDictionary(c => c.Key, c => c.Sum(p => groupScores.GetValueOrDefault(p.GroupId)));

        var withTurns = plants
            .GroupBy(p => p.GroupId)
            .SelectMany(g => g
                .OrderBy(p => p.Seen)
                .ThenBy(p => !p.HasImage)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ThenBy(p => p.Id)
                .Select((plant, turn) => (Plant: plant, Turn: turn)))
            .ToList();

        var strong = new Queue<Guid>(withTurns
            .Where(x => groupScores.GetValueOrDefault(x.Plant.GroupId) > 0)
            .OrderBy(x => x.Turn)
            .ThenByDescending(x => groupScores[x.Plant.GroupId])
            .ThenBy(x => x.Plant.GroupId)
            .Select(x => x.Plant.Id));

        var adjacent = new Queue<Guid>(withTurns
            .Where(x => groupScores.GetValueOrDefault(x.Plant.GroupId) <= 0 && categoryScores[x.Plant.CategoryId] > 0)
            .OrderBy(x => x.Turn)
            .ThenByDescending(x => categoryScores[x.Plant.CategoryId])
            .ThenBy(x => x.Plant.GroupId)
            .Select(x => x.Plant.Id));

        // Novel plants keep the default feed's shape: categories take turns, and so do the groups inside each.
        var novel = new Queue<Guid>(withTurns
            .Where(x => categoryScores[x.Plant.CategoryId] <= 0)
            .GroupBy(x => x.Plant.CategoryId)
            .SelectMany(c => c
                .OrderBy(x => x.Turn)
                .ThenBy(x => x.Plant.GroupId)
                .Select((x, categoryTurn) => (x.Plant, CategoryTurn: categoryTurn)))
            .OrderBy(x => x.CategoryTurn)
            .ThenBy(x => x.Plant.CategoryId)
            .Select(x => x.Plant.Id));

        var order = new List<Guid>(plants.Count);
        for (var slot = 0; order.Count < plants.Count; slot++)
        {
            var next = Pattern[slot % Pattern.Length] switch
            {
                'S' => Take(strong, adjacent, novel),
                'A' => Take(adjacent, strong, novel),
                _ => Take(novel, adjacent, strong)
            };
            order.Add(next);
        }

        return order;
    }

    private static Guid Take(Queue<Guid> first, Queue<Guid> second, Queue<Guid> third) =>
        first.Count > 0 ? first.Dequeue() : second.Count > 0 ? second.Dequeue() : third.Dequeue();
}
