using Presidents.Domain.Common;

namespace Presidents.Domain.Entities;

public sealed class Category : PublishableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public static Category Create(string name, string? description = null, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome da categoria é obrigatório.");

        var category = new Category
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Slug = TextNormalizer.Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug)
        };
        if (string.IsNullOrWhiteSpace(category.Slug))
            throw new DomainException("Não foi possível gerar o endereço da categoria.");
        category.RebuildSearchText();
        return category;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Name} {Description}");
}
