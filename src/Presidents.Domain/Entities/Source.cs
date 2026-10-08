using Presidents.Domain.Common;
using Presidents.Domain.Enums;

namespace Presidents.Domain.Entities;

public sealed class SourceRecord : PublishableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Url { get; set; }
    public SourceType SourceType { get; set; } = SourceType.Other;
    public string? Publisher { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public DateOnly? AccessedAt { get; set; }
    public ReliabilityLevel ReliabilityLevel { get; set; } = ReliabilityLevel.Secondary;
    public string? Notes { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public static SourceRecord Create(string name, SourceType type, ReliabilityLevel reliability, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("O nome da fonte é obrigatório.");

        var source = new SourceRecord
        {
            Name = name.Trim(),
            SourceType = type,
            ReliabilityLevel = reliability,
            Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            Slug = TextNormalizer.Slugify(name)
        };
        if (string.IsNullOrWhiteSpace(source.Slug))
            source.Slug = "fonte";
        source.RebuildSearchText();
        return source;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Name} {Title} {Publisher} {Author} {Url} {Notes}");
}

public sealed class FactSource : Entity
{
    public Guid SourceId { get; set; }
    public SourceRecord? Source { get; set; }
    public ContentEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? Excerpt { get; set; }
    public string? Annotation { get; set; }
    public DateTimeOffset LinkedAt { get; set; }

    public static FactSource Link(Guid sourceId, ContentEntityType entityType, Guid entityId, string? excerpt, string? annotation)
    {
        if (sourceId == Guid.Empty || entityId == Guid.Empty)
            throw new DomainException("A associação de fonte exige identificadores válidos.");
        if (excerpt is { Length: > FieldLimits.Excerpt })
            throw new DomainException($"O trecho citado não pode ultrapassar {FieldLimits.Excerpt} caracteres.");

        return new FactSource
        {
            SourceId = sourceId,
            EntityType = entityType,
            EntityId = entityId,
            Excerpt = string.IsNullOrWhiteSpace(excerpt) ? null : excerpt.Trim(),
            Annotation = string.IsNullOrWhiteSpace(annotation) ? null : annotation.Trim(),
            LinkedAt = DateTimeOffset.UtcNow
        };
    }
}

public sealed class SourcedStatement : PublishableEntity
{
    public ContentEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public StatementKind Kind { get; set; } = StatementKind.DocumentedFact;
    public string Text { get; set; } = string.Empty;
    public string? Attribution { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public static SourcedStatement Create(ContentEntityType entityType, Guid entityId, StatementKind kind, string text, string? attribution)
    {
        if (entityId == Guid.Empty)
            throw new DomainException("A afirmação precisa estar ligada a um registro.");
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("O texto da afirmação é obrigatório.");
        if (kind is StatementKind.Opinion or StatementKind.GroupCriticism or StatementKind.HistoricalInterpretation
            && string.IsNullOrWhiteSpace(attribution))
            throw new DomainException("Opinião, crítica e interpretação histórica exigem atribuição de quem as formula.");

        var statement = new SourcedStatement
        {
            EntityType = entityType,
            EntityId = entityId,
            Kind = kind,
            Text = text.Trim(),
            Attribution = string.IsNullOrWhiteSpace(attribution) ? null : attribution.Trim()
        };
        statement.RebuildSearchText();
        return statement;
    }

    public void RebuildSearchText() =>
        SearchText = TextNormalizer.Fold($"{Text} {Attribution}");
}

public sealed class SourceDivergence : AuditableEntity
{
    public ContentEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string Description { get; set; } = string.Empty;
}
