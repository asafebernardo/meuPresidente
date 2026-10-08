using Presidents.Domain.Common;
using Presidents.Domain.Rules;

namespace Presidents.Domain.Entities;

public sealed class President : PublishableEntity
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Party { get; set; }
    public string? VicePresident { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateOnly? DeathDate { get; set; }
    public string? Biography { get; set; }
    public string? HistoricalContext { get; set; }
    public string? PhotoUrl { get; set; }
    public string? WikipediaUrl { get; set; }
    public string? OfficialBiographyUrl { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public List<Presidency> Presidencies { get; set; } = [];

    public static President Create(string name, string fullName, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome do presidente é obrigatório.");
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("O nome completo do presidente é obrigatório.");

        var president = new President
        {
            Name = name.Trim(),
            FullName = fullName.Trim(),
            Slug = TextNormalizer.Slugify(string.IsNullOrWhiteSpace(slug) ? fullName : slug)
        };

        if (string.IsNullOrWhiteSpace(president.Slug))
            throw new DomainException("Não foi possível gerar um endereço amigável para o presidente.");

        president.RebuildSearchText();
        return president;
    }

    public void EnsureInvariants()
    {
        DateRules.EnsureBirthDeath(BirthDate, DeathDate);
        DateRules.EnsurePeriod(StartDate, EndDate, "resumo de mandato");
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Slug))
            throw new DomainException("Nome, nome completo e endereço amigável são obrigatórios.");
    }

    public void RefreshSummary(IEnumerable<Presidency> mandates)
    {
        var ordered = mandates.OrderBy(m => m.StartDate).ToList();
        if (ordered.Count == 0)
            return;

        StartDate = ordered[0].StartDate;
        EndDate = ordered[^1].EndDate;
        if (!string.IsNullOrWhiteSpace(ordered[^1].Party))
            Party = ordered[^1].Party;
        if (!string.IsNullOrWhiteSpace(ordered[^1].VicePresident))
            VicePresident = ordered[^1].VicePresident;
    }

    public void RebuildSearchText()
    {
        SearchText = TextNormalizer.Fold(string.Join(' ',
            Name, FullName, Party, VicePresident, Biography, HistoricalContext));
    }
}
