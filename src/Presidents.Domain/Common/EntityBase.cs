using Presidents.Domain.Enums;

namespace Presidents.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public abstract class PublishableEntity : AuditableEntity
{
    public PublicationStatus Status { get; set; } = PublicationStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DataProvenance Provenance { get; set; } = DataProvenance.Editorial;

    public bool IsPublic => Status == PublicationStatus.Published;
}
