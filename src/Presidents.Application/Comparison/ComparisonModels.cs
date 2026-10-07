using Presidents.Domain.Common;

namespace Presidents.Application.Comparison;

public sealed record ComparisonColumn(
    Guid Id,
    string Name,
    string Slug,
    string? Party,
    string? VicePresident,
    string Mandate,
    int LawCount,
    int EventCount,
    int PolicyCount);

public sealed record IndicatorComparisonRow(
    string Indicator,
    string Unit,
    IReadOnlyList<string> Values);

public sealed record ChartPoint(int Year, decimal Value);

public sealed record ChartSeries(string Indicator, string Unit, IReadOnlyList<IReadOnlyList<ChartPoint>> PointsByColumn);

public sealed record ComparisonResult(
    string Disclaimer,
    IReadOnlyList<ComparisonColumn> Columns,
    IReadOnlyList<IndicatorComparisonRow> Indicators,
    IReadOnlyList<ChartSeries> Charts);

public sealed class PresidentSlice
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Party { get; set; }
    public string? VicePresident { get; init; }
    public string Mandate { get; init; } = "Sem mandato publicado";
    public int LawCount { get; init; }
    public int EventCount { get; init; }
    public int PolicyCount { get; init; }
    public Dictionary<string, List<(int Year, decimal Value, string Unit)>> IndicatorValues { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class ComparisonAssembler
{
    public const string Disclaimer =
        "Esta comparação organiza dados publicados lado a lado. Ela não produz ranking nem classifica presidentes como melhores ou piores.";

    public static ComparisonResult Assemble(IReadOnlyList<PresidentSlice> slices)
    {
        if (slices.Count > 4)
            throw new DomainException("A comparação aceita no máximo 4 presidentes.");

        var columns = slices.Select(slice => new ComparisonColumn(
            slice.Id,
            slice.Name,
            slice.Slug,
            slice.Party,
            slice.VicePresident,
            slice.Mandate,
            slice.LawCount,
            slice.EventCount,
            slice.PolicyCount)).ToList();

        var names = slices
            .SelectMany(slice => slice.IndicatorValues.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<IndicatorComparisonRow>();
        var charts = new List<ChartSeries>();
        foreach (var name in names)
        {
            var points = slices.Select(slice => slice.IndicatorValues.GetValueOrDefault(name) ?? []).ToList();
            var unit = points.SelectMany(series => series).Select(point => point.Unit).FirstOrDefault() ?? string.Empty;
            rows.Add(new IndicatorComparisonRow(name, unit, points.Select(Format).ToList()));
            charts.Add(new ChartSeries(name, unit, points.Select(series => (IReadOnlyList<ChartPoint>)series.Select(point => new ChartPoint(point.Year, point.Value)).ToList()).ToList()));
        }

        return new ComparisonResult(Disclaimer, columns, rows, charts);
    }

    private static string Format(List<(int Year, decimal Value, string Unit)>? points)
    {
        if (points is null || points.Count == 0)
            return "Sem valor publicado";

        return string.Join("; ", points.OrderBy(point => point.Year).Select(point => $"{point.Year}: {point.Value}"));
    }
}
