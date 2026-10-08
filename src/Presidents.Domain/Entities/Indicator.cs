using Presidents.Domain.Common;

namespace Presidents.Domain.Entities;

public sealed class Indicator : PublishableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? MethodologySourceId { get; set; }
    public SourceRecord? MethodologySource { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public List<PresidentIndicator> Values { get; set; } = [];

    public static Indicator Create(string name, string unit, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do indicador é obrigatório.");
        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("A unidade do indicador é obrigatória.");

        var indicator = new Indicator
        {
            Name = name.Trim(),
            Unit = unit.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Slug = TextNormalizer.Slugify(name)
        };
        indicator.RebuildSearchText();
        return indicator;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Name} {Unit} {Description}");
}

public sealed class PresidentIndicator : PublishableEntity
{
    public Guid PresidentId { get; set; }
    public President? President { get; set; }
    public Guid IndicatorId { get; set; }
    public Indicator? Indicator { get; set; }
    public int Year { get; set; }
    public decimal Value { get; set; }
    public Guid SourceId { get; set; }
    public SourceRecord? Source { get; set; }
    public string? Note { get; set; }

    public static PresidentIndicator Create(Guid presidentId, Guid indicatorId, int year, decimal value, Guid sourceId)
    {
        if (presidentId == Guid.Empty || indicatorId == Guid.Empty || sourceId == Guid.Empty)
            throw new DomainException("O valor de indicador exige presidente, indicador e fonte.");
        if (year is < 1889 or > 2100)
            throw new DomainException("O ano do indicador está fora do intervalo aceito.");

        return new PresidentIndicator
        {
            PresidentId = presidentId,
            IndicatorId = indicatorId,
            Year = year,
            Value = value,
            SourceId = sourceId
        };
    }
}
