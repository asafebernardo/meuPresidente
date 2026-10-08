using Presidents.Domain.Enums;

namespace Presidents.Application.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record PublicCounts(
    int Presidents,
    int Presidencies,
    int Events,
    int Laws,
    int Policies,
    int Sources,
    int Indicators,
    int Categories);

public sealed record AdminStatsDto(
    int Presidents,
    int Presidencies,
    int Events,
    int Laws,
    int Policies,
    int Indicators,
    int Categories,
    int Sources,
    int Drafts);

public sealed record SourceCitationDto(
    Guid Id,
    Guid SourceId,
    string Name,
    string? Url,
    string? Publisher,
    string? Author,
    string? Title,
    DateOnly? PublicationDate,
    DateOnly? AccessedAt,
    string SourceType,
    string Reliability,
    string? Excerpt,
    string? Annotation);

public sealed record CategoryDto(Guid Id, string Name, string Slug, string? Description);

public sealed record StatementDto(
    Guid Id,
    string Kind,
    StatementKind KindCode,
    string Text,
    string? Attribution,
    IReadOnlyList<SourceCitationDto> Sources);

public sealed record PresidencyDto(
    Guid Id,
    int Ordinal,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? VicePresident,
    string? Party,
    string GovernmentType,
    string ArrivalMethod,
    string? PoliticalContext,
    string? DivergenceNote,
    bool Demonstration,
    IReadOnlyList<SourceCitationDto> Sources);

public sealed record EventCardDto(
    Guid Id,
    string Title,
    string Slug,
    DateOnly? EventDate,
    DateOnly? EndDate,
    string EventType,
    string Importance,
    string SummaryKind,
    string? NeutralSummary,
    string? Description,
    string? DivergenceNote,
    string? PresidentName,
    string? PresidentSlug,
    IReadOnlyList<string> Categories,
    bool Demonstration,
    IReadOnlyList<SourceCitationDto> Sources);

public sealed record LawCardDto(
    Guid Id,
    string Number,
    int Year,
    string Kind,
    string Slug,
    string Title,
    string? Summary,
    string OperationalStatus,
    string Origin,
    DateOnly? SanctionDate,
    DateOnly? PublicationDate,
    DateOnly? EffectiveDate,
    DateOnly? PromulgationDate,
    DateOnly? VetoDate,
    string? FullTextUrl,
    string? Proposer,
    bool AttributedAsAuthor,
    string? PresidentInOffice,
    string? PresidentSlug,
    string? ProcessNote,
    string? VetoNote,
    string? DivergenceNote,
    IReadOnlyList<string> Categories,
    bool Demonstration,
    IReadOnlyList<SourceCitationDto> Sources);

public sealed record PolicyCardDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? Objective,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? PresidentName,
    string? PresidentSlug,
    IReadOnlyList<string> Categories,
    IReadOnlyList<StatementDto> Results,
    IReadOnlyList<StatementDto> Criticisms,
    IReadOnlyList<StatementDto> Assessments,
    bool Demonstration,
    IReadOnlyList<SourceCitationDto> Sources);

public sealed record IndicatorPointDto(
    Guid Id,
    string Indicator,
    string Unit,
    int Year,
    decimal Value,
    string? Note,
    SourceCitationDto? Source);

public sealed record PresidentListItemDto(
    Guid Id,
    string Name,
    string FullName,
    string Slug,
    string? PhotoUrl,
    string Mandate,
    string? Party,
    string? VicePresident,
    bool Demonstration);

public sealed record PresidentDetailDto(
    Guid Id,
    string Name,
    string FullName,
    string Slug,
    string? PhotoUrl,
    string Mandate,
    string PartyCaption,
    string? Party,
    string? VicePresident,
    DateOnly? BirthDate,
    DateOnly? DeathDate,
    string? Biography,
    string? HistoricalContext,
    string? WikipediaUrl,
    string? OfficialBiographyUrl,
    bool Demonstration,
    string Provenance,
    IReadOnlyList<PresidencyDto> Presidencies,
    IReadOnlyList<EventCardDto> Events,
    IReadOnlyList<LawCardDto> Laws,
    IReadOnlyList<PolicyCardDto> Policies,
    IReadOnlyList<StatementDto> Statements,
    IReadOnlyList<IndicatorPointDto> Indicators,
    IReadOnlyList<SourceCitationDto> Sources,
    IReadOnlyList<CategorySectionDto> Sections,
    IReadOnlyList<TopicCountDto> LawTopics,
    int LawTotal,
    IReadOnlyList<MandateMetricDto> PeriodMetrics);

public sealed record CategorySectionDto(string Slug, string Name, IReadOnlyList<EventCardDto> Events, IReadOnlyList<PolicyCardDto> Policies);

public sealed record TimelineMandateDto(
    Guid PresidentId,
    string PresidentName,
    string Slug,
    string? Party,
    DateOnly StartDate,
    DateOnly? EndDate,
    string GovernmentType,
    string ArrivalMethod,
    int Ordinal);

public sealed record TimelineEventDto(string Title, string Slug, DateOnly? EventDate, string Importance, string? PresidentName);

public sealed record TimelineDto(IReadOnlyList<TimelineMandateDto> Mandates, IReadOnlyList<TimelineEventDto> Events);

public sealed record TopicCountDto(Guid Id, string Name, string Slug, int LawCount);

public sealed record PresidentLawShareDto(Guid Id, string Name, string Slug, string Mandate, int LawCount);

public sealed record MandateMetricDto(
    string PresidentName,
    string PresidentSlug,
    string MandateLabel,
    string Indicator,
    string Text,
    string SourceName,
    string? SourceUrl);

public sealed record AreaPanelDto(
    string Name,
    string Slug,
    string SeriesNote,
    IReadOnlyList<MandateMetricDto> Rows);

public sealed record HomePageDto(
    IReadOnlyList<PresidentListItemDto> Presidents,
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<EventCardDto> RecentEvents,
    IReadOnlyList<LawCardDto> Laws,
    IReadOnlyList<TimelineMandateDto> Timeline,
    PublicCounts Counts,
    int LawsSinceRedemocratization,
    IReadOnlyList<TopicCountDto> Topics,
    IReadOnlyList<PresidentLawShareDto> LawShares,
    IReadOnlyList<AreaPanelDto> Areas);

public sealed class PresidentForm
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public DateOnly? BirthDate { get; set; }
    public DateOnly? DeathDate { get; set; }
    public string? Biography { get; set; }
    public string? HistoricalContext { get; set; }
    public string? PhotoUrl { get; set; }
    public string? WikipediaUrl { get; set; }
    public string? OfficialBiographyUrl { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Party { get; set; }
    public string? VicePresident { get; set; }
}

public sealed class PresidencyForm
{
    public Guid? Id { get; set; }
    public Guid PresidentId { get; set; }
    public int Ordinal { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? VicePresident { get; set; }
    public string? Party { get; set; }
    public string? PoliticalContext { get; set; }
    public GovernmentType GovernmentType { get; set; } = GovernmentType.NotInformed;
    public ArrivalMethod ArrivalMethod { get; set; } = ArrivalMethod.NotInformed;
    public string? DivergenceNote { get; set; }
}

public sealed class CategoryForm
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class EventForm
{
    public Guid? Id { get; set; }
    public Guid? PresidentId { get; set; }
    public Guid? PresidencyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public DateOnly? EventDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ImportanceLevel Importance { get; set; } = ImportanceLevel.Notable;
    public EventType EventType { get; set; } = EventType.Other;
    public string? NeutralSummary { get; set; }
    public StatementKind SummaryKind { get; set; } = StatementKind.DocumentedFact;
    public string? DivergenceNote { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class LawForm
{
    public Guid? Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Year { get; set; }
    public NormKind Kind { get; set; } = NormKind.OrdinaryLaw;
    public string Title { get; set; } = string.Empty;
    public string? Slug { get; set; }
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
    public bool AttributeAuthorship { get; set; }
    public string? LegislativeProcessNote { get; set; }
    public string? VetoNote { get; set; }
    public string? DivergenceNote { get; set; }
    public Guid? PresidentInOfficeId { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class PolicyForm
{
    public Guid? Id { get; set; }
    public Guid? PresidentId { get; set; }
    public Guid? PresidencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Objective { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}

public sealed class SourceForm
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Url { get; set; }
    public SourceType SourceType { get; set; } = SourceType.Other;
    public string? Publisher { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public string? Author { get; set; }
    public string? Title { get; set; }
    public DateOnly? AccessedAt { get; set; }
    public ReliabilityLevel ReliabilityLevel { get; set; } = ReliabilityLevel.Secondary;
    public string? Notes { get; set; }
}

public sealed class IndicatorForm
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? MethodologySourceId { get; set; }
}

public sealed class IndicatorValueForm
{
    public Guid? Id { get; set; }
    public Guid PresidentId { get; set; }
    public Guid IndicatorId { get; set; }
    public int Year { get; set; }
    public decimal Value { get; set; }
    public Guid SourceId { get; set; }
    public string? Note { get; set; }
}

public sealed class StatementForm
{
    public Guid? Id { get; set; }
    public ContentEntityType EntityType { get; set; } = ContentEntityType.President;
    public Guid EntityId { get; set; }
    public StatementKind Kind { get; set; } = StatementKind.DocumentedFact;
    public string Text { get; set; } = string.Empty;
    public string? Attribution { get; set; }
}

public sealed class FactSourceForm
{
    public Guid? Id { get; set; }
    public Guid SourceId { get; set; }
    public ContentEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public string? Excerpt { get; set; }
    public string? Annotation { get; set; }
}

public sealed class StatusForm
{
    public PublicationStatus Status { get; set; }
}

public sealed record NamedOption(Guid Id, string Name);

public sealed class LawQuery
{
    public bool PublishedOnly { get; set; } = true;
    public Guid? PresidentId { get; set; }
    public Guid? CategoryId { get; set; }
    public int? Year { get; set; }
    public NormKind? Kind { get; set; }
    public LawOperationalStatus? OperationalStatus { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? Term { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class EventQuery
{
    public bool PublishedOnly { get; set; } = true;
    public Guid? PresidentId { get; set; }
    public Guid? CategoryId { get; set; }
    public EventType? EventType { get; set; }
    public int? Year { get; set; }
    public string? Term { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ImportRequest
{
    public ImportChannel Channel { get; set; } = ImportChannel.Legislation;
    public string? Url { get; set; }
    public string? ExternalId { get; set; }
    public string? Title { get; set; }
    public string? Publisher { get; set; }
    public string? Author { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public string? Summary { get; set; }
    public string? Excerpt { get; set; }
    public string? RawContent { get; set; }
    public string? Number { get; set; }
    public int? Year { get; set; }
    public NormKind Kind { get; set; } = NormKind.OrdinaryLaw;
    public Guid? PresidentInOfficeId { get; set; }
}

public sealed record ImportResultDto(bool Created, bool Duplicate, Guid? RecordId, string Message);

public sealed record SourcePageDto(
    Guid Id,
    string Name,
    string Slug,
    string? Url,
    string SourceType,
    string Reliability,
    string? Publisher,
    string? Author,
    string? Title,
    DateOnly? PublicationDate,
    DateOnly? AccessedAt,
    string? Notes,
    bool Demonstration);

public sealed record LoginResultDto(string AccessToken, DateTimeOffset ExpiresAt, string Email, IReadOnlyList<string> Roles);

public interface IAuthService
{
    Task<LoginResultDto?> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public interface IDataImporter
{
    string AdapterName { get; }
    ImportChannel Channel { get; }
    Task<ImportResultDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken);
}
