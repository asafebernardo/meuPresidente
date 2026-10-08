namespace Presidents.Application.Search;

public enum SearchHitKind
{
    President = 1,
    Law = 2,
    Event = 3,
    Policy = 4,
    Source = 5
}

public sealed record SearchHit(
    Guid Id,
    SearchHitKind Kind,
    string Title,
    string? Subtitle,
    string Url,
    string? Excerpt);

public sealed record SearchResponse(
    string Query,
    IReadOnlyList<SearchHit> Presidents,
    IReadOnlyList<SearchHit> Laws,
    IReadOnlyList<SearchHit> Events,
    IReadOnlyList<SearchHit> Policies,
    IReadOnlyList<SearchHit> Sources)
{
    public int Total => Presidents.Count + Laws.Count + Events.Count + Policies.Count + Sources.Count;
}

public static class SearchGrouper
{
    public static SearchResponse Group(string query, IReadOnlyList<SearchHit> hits) => new(
        query.Trim(),
        Of(hits, SearchHitKind.President),
        Of(hits, SearchHitKind.Law),
        Of(hits, SearchHitKind.Event),
        Of(hits, SearchHitKind.Policy),
        Of(hits, SearchHitKind.Source));

    private static IReadOnlyList<SearchHit> Of(IReadOnlyList<SearchHit> hits, SearchHitKind kind) =>
        hits.Where(hit => hit.Kind == kind).ToList();
}
