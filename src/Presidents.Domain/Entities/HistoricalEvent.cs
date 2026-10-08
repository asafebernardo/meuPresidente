using Presidents.Domain.Common;
using Presidents.Domain.Enums;
using Presidents.Domain.Rules;

namespace Presidents.Domain.Entities;

public sealed class HistoricalEvent : PublishableEntity
{
    public Guid? PresidentId { get; set; }
    public President? President { get; set; }
    public Guid? PresidencyId { get; set; }
    public Presidency? Presidency { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? EventDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ImportanceLevel Importance { get; set; } = ImportanceLevel.Notable;
    public EventType EventType { get; set; } = EventType.Other;
    public string? NeutralSummary { get; set; }
    public StatementKind SummaryKind { get; set; } = StatementKind.DocumentedFact;
    public string? DivergenceNote { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public static HistoricalEvent Create(string title, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("O título do acontecimento é obrigatório.");

        var ev = new HistoricalEvent
        {
            Title = title.Trim(),
            Slug = TextNormalizer.Slugify(string.IsNullOrWhiteSpace(slug) ? title : slug)
        };
        if (string.IsNullOrWhiteSpace(ev.Slug))
            throw new DomainException("Não foi possível gerar o endereço do acontecimento.");
        return ev;
    }

    public void EnsureInvariants()
    {
        DateRules.EnsurePeriod(EventDate, EndDate, "acontecimento");
        if (string.IsNullOrWhiteSpace(Title))
            throw new DomainException("O título do acontecimento é obrigatório.");
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Title} {Description} {NeutralSummary} {DivergenceNote}");
}
