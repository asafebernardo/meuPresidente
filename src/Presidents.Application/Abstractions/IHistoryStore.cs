using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Rules;

namespace Presidents.Application.Abstractions;

public interface IHistoryStore
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<President?> FindPresidentAsync(Guid id, CancellationToken cancellationToken);
    Task<President?> FindPresidentBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<PagedResult<President>> PagePresidentsAsync(bool publishedOnly, string? term, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> PresidentSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    Task<int> CountPresidentBlockersAsync(Guid id, CancellationToken cancellationToken);
    void Add(President entity);
    void Remove(President entity);

    Task<Presidency?> FindPresidencyAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Presidency>> ListPresidenciesAsync(bool publishedOnly, Guid? presidentId, CancellationToken cancellationToken);
    void Add(Presidency entity);
    void Remove(Presidency entity);

    Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken);
    Task<Category?> FindCategoryBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<IReadOnlyList<Category>> ListCategoriesAsync(bool publishedOnly, CancellationToken cancellationToken);
    Task<bool> CategorySlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    Task<int> CountCategoryLinksAsync(Guid id, CancellationToken cancellationToken);
    void Add(Category entity);
    void Remove(Category entity);

    Task<HistoricalEvent?> FindEventAsync(Guid id, CancellationToken cancellationToken);
    Task<HistoricalEvent?> FindEventBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<PagedResult<HistoricalEvent>> PageEventsAsync(EventQuery query, CancellationToken cancellationToken);
    Task<bool> EventSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    void Add(HistoricalEvent entity);
    void Remove(HistoricalEvent entity);

    Task<Law?> FindLawAsync(Guid id, CancellationToken cancellationToken);
    Task<Law?> FindLawBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<PagedResult<Law>> PageLawsAsync(LawQuery query, CancellationToken cancellationToken);
    Task<bool> LawSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    Task<bool> LawIdentityExistsAsync(NormKind kind, string number, int year, Guid? exceptId, CancellationToken cancellationToken);
    void Add(Law entity);
    void Remove(Law entity);

    Task<Policy?> FindPolicyAsync(Guid id, CancellationToken cancellationToken);
    Task<Policy?> FindPolicyBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<PagedResult<Policy>> PagePoliciesAsync(bool publishedOnly, Guid? presidentId, string? term, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> PolicySlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    void Add(Policy entity);
    void Remove(Policy entity);

    Task<SourceRecord?> FindSourceAsync(Guid id, CancellationToken cancellationToken);
    Task<SourceRecord?> FindSourceBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken);
    Task<SourceRecord?> FindSourceByUrlAsync(string url, CancellationToken cancellationToken);
    Task<PagedResult<SourceRecord>> PageSourcesAsync(bool publishedOnly, string? term, int page, int pageSize, CancellationToken cancellationToken);
    Task<bool> SourceSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    Task<int> CountSourceUsageAsync(Guid id, CancellationToken cancellationToken);
    void Add(SourceRecord entity);
    void Remove(SourceRecord entity);

    Task<Indicator?> FindIndicatorAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Indicator>> ListIndicatorsAsync(bool publishedOnly, CancellationToken cancellationToken);
    Task<bool> IndicatorSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken);
    void Add(Indicator entity);
    void Remove(Indicator entity);

    Task<PresidentIndicator?> FindIndicatorValueAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PresidentIndicator>> ListIndicatorValuesAsync(bool publishedOnly, Guid? presidentId, CancellationToken cancellationToken);
    Task<bool> IndicatorValueExistsAsync(Guid presidentId, Guid indicatorId, int year, Guid? exceptId, CancellationToken cancellationToken);
    void Add(PresidentIndicator entity);
    void Remove(PresidentIndicator entity);

    Task<SourcedStatement?> FindStatementAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourcedStatement>> ListStatementsAsync(ContentEntityType? type, Guid? entityId, bool publishedOnly, CancellationToken cancellationToken);
    void Add(SourcedStatement entity);
    void Remove(SourcedStatement entity);

    Task<IReadOnlyList<FactSource>> ListFactSourcesAsync(ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FactSource>> ListFactSourcesForAsync(ContentEntityType type, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken);
    Task<FactSource?> FindFactSourceAsync(Guid id, CancellationToken cancellationToken);
    Task<int> CountPublishedSourcesAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken);
    void Add(FactSource entity);
    void Remove(FactSource entity);

    Task ReplaceCategoriesAsync(ContentEntityType type, Guid entityId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentCategoryLink>> ListCategoryLinksAsync(ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContentCategoryLink>> ListCategoryLinksForAsync(ContentEntityType type, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<SourceDivergence>> ListDivergencesAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken);
    void Add(SourceDivergence entity);
    Task RemoveOwnedLinksAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken);

    Task<IReadOnlyList<IngestionFingerprint>> ListIngestionFingerprintsAsync(string adapter, CancellationToken cancellationToken);
    Task<IReadOnlyList<IngestionRecord>> ListIngestionAsync(int take, CancellationToken cancellationToken);
    void Add(IngestionRecord entity);

    Task<AdminStatsDto> AdminStatsAsync(CancellationToken cancellationToken);
    Task<PublicCounts> PublicCountsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchHitRow>> SearchRowsAsync(string foldedTerm, string digits, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceItem>> EvidenceAsync(string foldedTerm, int take, CancellationToken cancellationToken);
    Task<bool> EntityExistsAsync(ContentEntityType type, Guid id, CancellationToken cancellationToken);
}

public sealed record SearchHitRow(Guid Id, string Kind, string Title, string? Subtitle, string Slug, string? Excerpt);
