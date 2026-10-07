using Presidents.Domain.Common;
using Presidents.Domain.Enums;

namespace Presidents.Domain.Entities;

/// <summary>
/// Ato normativo. O presidente em exercício não é tratado como autor.
/// </summary>
public sealed class Law : PublishableEntity
{
    public string Number { get; set; } = string.Empty;
    public string NumberNormalized { get; set; } = string.Empty;
    public int Year { get; set; }
    public NormKind Kind { get; set; } = NormKind.OrdinaryLaw;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? FullTextUrl { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public DateOnly? SanctionDate { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? VetoDate { get; set; }
    public DateOnly? PromulgationDate { get; set; }
    public LawOperationalStatus OperationalStatus { get; set; } = LawOperationalStatus.Unknown;
    public NormOrigin Origin { get; set; } = NormOrigin.Unknown;
    public string? Proposer { get; set; }
    public bool AttributedAsAuthor { get; set; }
    public string? LegislativeProcessNote { get; set; }
    public string? VetoNote { get; set; }
    public string? DivergenceNote { get; set; }
    public Guid? PresidentInOfficeId { get; set; }
    public President? PresidentInOffice { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public static Law Create(NormKind kind, string number, int year, string title)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new DomainException("O número do ato normativo é obrigatório.");
        if (year is < 1800 or > 2100)
            throw new DomainException("O ano do ato normativo está fora do intervalo aceito.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("O título ou a ementa do ato é obrigatório.");

        var law = new Law
        {
            Kind = kind,
            Number = number.Trim(),
            NumberNormalized = TextNormalizer.Digits(number),
            Year = year,
            Title = title.Trim(),
            AttributedAsAuthor = false
        };
        law.Slug = TextNormalizer.Slugify($"{kind} {number} {year}");
        law.RebuildSearchText();
        return law;
    }

    public void AttributeAuthorship(string proposer)
    {
        if (string.IsNullOrWhiteSpace(proposer))
            throw new DomainException("A autoria exige o nome do autor ou proponente.");

        Proposer = proposer.Trim();
        AttributedAsAuthor = true;
    }

    public void ClearAuthorship()
    {
        AttributedAsAuthor = false;
        Proposer = null;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Kind} {Number} {NumberNormalized} {Year} {Title} {Summary} {Proposer}");
}
