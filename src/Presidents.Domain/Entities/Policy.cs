using Presidents.Domain.Common;

namespace Presidents.Domain.Entities;

public sealed class Policy : PublishableEntity
{
    public Guid? PresidentId { get; set; }
    public President? President { get; set; }
    public Guid? PresidencyId { get; set; }
    public Presidency? Presidency { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Objective { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public static Policy Create(string name, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome da política pública é obrigatório.");

        var policy = new Policy
        {
            Name = name.Trim(),
            Slug = TextNormalizer.Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug)
        };
        if (string.IsNullOrWhiteSpace(policy.Slug))
            throw new DomainException("Não foi possível gerar o endereço da política pública.");
        return policy;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Name} {Description} {Objective}");
}
