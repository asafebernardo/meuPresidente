using Presidents.Domain.Common;
using Presidents.Domain.Enums;

namespace Presidents.Domain.Entities;

public sealed class ContentCategoryLink : Entity
{
    public ContentEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class IngestionRecord : AuditableEntity
{
    public string AdapterName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? ExternalId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public DateTimeOffset CollectedAt { get; set; }
    public string? Summary { get; set; }
    public string? RawContent { get; set; }
    public Guid? SourceId { get; set; }
    public SourceRecord? Source { get; set; }
    public Guid? LawId { get; set; }
    public Law? Law { get; set; }
}
