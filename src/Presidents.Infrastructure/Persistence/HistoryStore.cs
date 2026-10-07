using Microsoft.EntityFrameworkCore;
using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;
using Presidents.Domain.Rules;

namespace Presidents.Infrastructure.Persistence;

public sealed class HistoryStore(AppDbContext db) : IHistoryStore
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public Task<President?> FindPresidentAsync(Guid id, CancellationToken cancellationToken) =>
        db.Presidents.Include(president => president.Presidencies).FirstOrDefaultAsync(president => president.Id == id, cancellationToken);

    public Task<President?> FindPresidentBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Presidents.AsNoTracking().Include(president => president.Presidencies).AsQueryable();
        if (publishedOnly)
            query = query.Where(president => president.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(president => president.Slug == slug, cancellationToken);
    }

    public async Task<PagedResult<President>> PagePresidentsAsync(bool publishedOnly, string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (current, size, skip) = Page(page, pageSize);
        var query = db.Presidents.AsNoTracking().Include(president => president.Presidencies).AsQueryable();
        if (publishedOnly)
            query = query.Where(president => president.Status == PublicationStatus.Published);
        query = ApplyTerm(query, term, president => president.SearchText);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(president => president.StartDate).ThenBy(president => president.FullName).Skip(skip).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<President>(items, current, size, total);
    }

    public Task<bool> PresidentSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Presidents.AnyAsync(president => president.Slug == slug && president.Id != exceptId, cancellationToken);

    public async Task<int> CountPresidentBlockersAsync(Guid id, CancellationToken cancellationToken)
    {
        var events = await db.HistoricalEvents.CountAsync(item => item.PresidentId == id, cancellationToken);
        var laws = await db.Laws.CountAsync(item => item.PresidentInOfficeId == id, cancellationToken);
        var policies = await db.Policies.CountAsync(item => item.PresidentId == id, cancellationToken);
        return events + laws + policies;
    }

    public void Add(President entity) => db.Presidents.Add(entity);
    public void Remove(President entity) => db.Presidents.Remove(entity);

    public Task<Presidency?> FindPresidencyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Presidencies.Include(mandate => mandate.President).FirstOrDefaultAsync(mandate => mandate.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Presidency>> ListPresidenciesAsync(bool publishedOnly, Guid? presidentId, CancellationToken cancellationToken)
    {
        var query = db.Presidencies.AsNoTracking().Include(mandate => mandate.President).AsQueryable();
        if (publishedOnly)
            query = query.Where(mandate => mandate.Status == PublicationStatus.Published);
        if (presidentId is { } id)
            query = query.Where(mandate => mandate.PresidentId == id);
        return await query.OrderBy(mandate => mandate.StartDate).ThenBy(mandate => mandate.Ordinal).ToListAsync(cancellationToken);
    }

    public void Add(Presidency entity) => db.Presidencies.Add(entity);
    public void Remove(Presidency entity) => db.Presidencies.Remove(entity);

    public Task<Category?> FindCategoryAsync(Guid id, CancellationToken cancellationToken) =>
        db.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<Category?> FindCategoryBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (publishedOnly)
            query = query.Where(category => category.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(category => category.Slug == slug, cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> ListCategoriesAsync(bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Categories.AsNoTracking().AsQueryable();
        if (publishedOnly)
            query = query.Where(category => category.Status == PublicationStatus.Published);
        return await query.OrderBy(category => category.SortOrder).ThenBy(category => category.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> CategorySlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Categories.AnyAsync(category => category.Slug == slug && category.Id != exceptId, cancellationToken);

    public Task<int> CountCategoryLinksAsync(Guid id, CancellationToken cancellationToken) =>
        db.CategoryLinks.CountAsync(link => link.CategoryId == id, cancellationToken);

    public void Add(Category entity) => db.Categories.Add(entity);
    public void Remove(Category entity) => db.Categories.Remove(entity);

    public Task<HistoricalEvent?> FindEventAsync(Guid id, CancellationToken cancellationToken) =>
        db.HistoricalEvents.Include(item => item.President).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<HistoricalEvent?> FindEventBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.HistoricalEvents.AsNoTracking().Include(item => item.President).AsQueryable();
        if (publishedOnly)
            query = query.Where(item => item.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);
    }

    public async Task<PagedResult<HistoricalEvent>> PageEventsAsync(EventQuery query, CancellationToken cancellationToken)
    {
        var (current, size, skip) = Page(query.Page, query.PageSize);
        var rows = db.HistoricalEvents.AsNoTracking().Include(item => item.President).AsQueryable();
        if (query.PublishedOnly)
            rows = rows.Where(item => item.Status == PublicationStatus.Published);
        if (query.PresidentId is { } presidentId)
            rows = rows.Where(item => item.PresidentId == presidentId);
        if (query.EventType is { } eventType)
            rows = rows.Where(item => item.EventType == eventType);
        if (query.Year is { } year)
            rows = rows.Where(item => item.EventDate != null && item.EventDate.Value.Year == year);
        if (query.CategoryId is { } categoryId)
        {
            var ids = db.CategoryLinks.Where(link => link.EntityType == ContentEntityType.HistoricalEvent && link.CategoryId == categoryId).Select(link => link.EntityId);
            rows = rows.Where(item => ids.Contains(item.Id));
        }
        rows = ApplyTerm(rows, query.Term, item => item.SearchText);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(item => item.EventDate).ThenBy(item => item.Title).Skip(skip).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<HistoricalEvent>(items, current, size, total);
    }

    public Task<bool> EventSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.HistoricalEvents.AnyAsync(item => item.Slug == slug && item.Id != exceptId, cancellationToken);

    public void Add(HistoricalEvent entity) => db.HistoricalEvents.Add(entity);
    public void Remove(HistoricalEvent entity) => db.HistoricalEvents.Remove(entity);

    public Task<Law?> FindLawAsync(Guid id, CancellationToken cancellationToken) =>
        db.Laws.Include(law => law.PresidentInOffice).FirstOrDefaultAsync(law => law.Id == id, cancellationToken);

    public Task<Law?> FindLawBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Laws.AsNoTracking().Include(law => law.PresidentInOffice).AsQueryable();
        if (publishedOnly)
            query = query.Where(law => law.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(law => law.Slug == slug, cancellationToken);
    }

    public async Task<PagedResult<Law>> PageLawsAsync(LawQuery query, CancellationToken cancellationToken)
    {
        var (current, size, skip) = Page(query.Page, query.PageSize);
        var rows = db.Laws.AsNoTracking().Include(law => law.PresidentInOffice).AsQueryable();
        if (query.PublishedOnly)
            rows = rows.Where(law => law.Status == PublicationStatus.Published);
        if (query.PresidentId is { } presidentId)
            rows = rows.Where(law => law.PresidentInOfficeId == presidentId);
        if (query.Year is { } year)
            rows = rows.Where(law => law.Year == year);
        if (query.Kind is { } kind)
            rows = rows.Where(law => law.Kind == kind);
        if (query.OperationalStatus is { } status)
            rows = rows.Where(law => law.OperationalStatus == status);
        if (query.From is { } from)
            rows = rows.Where(law => (law.SanctionDate ?? law.PublicationDate) >= from);
        if (query.To is { } to)
            rows = rows.Where(law => (law.SanctionDate ?? law.PublicationDate) <= to);
        if (query.CategoryId is { } categoryId)
        {
            var ids = db.CategoryLinks.Where(link => link.EntityType == ContentEntityType.Law && link.CategoryId == categoryId).Select(link => link.EntityId);
            rows = rows.Where(law => ids.Contains(law.Id));
        }

        var folded = TextNormalizer.Fold(query.Term);
        var digits = TextNormalizer.Digits(query.Term);
        if (folded.Length > 0)
            rows = rows.Where(law => law.SearchText.Contains(folded) || (digits.Length > 0 && law.NumberNormalized.Contains(digits)));

        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderByDescending(law => law.Year).ThenBy(law => law.Number).Skip(skip).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<Law>(items, current, size, total);
    }

    public Task<bool> LawSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Laws.AnyAsync(law => law.Slug == slug && law.Id != exceptId, cancellationToken);

    public Task<bool> LawIdentityExistsAsync(NormKind kind, string number, int year, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = TextNormalizer.Digits(number);
        return db.Laws.AnyAsync(law => law.Kind == kind && law.Year == year && law.NumberNormalized == normalized && law.Id != exceptId, cancellationToken);
    }

    public void Add(Law entity) => db.Laws.Add(entity);
    public void Remove(Law entity) => db.Laws.Remove(entity);

    public Task<Policy?> FindPolicyAsync(Guid id, CancellationToken cancellationToken) =>
        db.Policies.Include(policy => policy.President).FirstOrDefaultAsync(policy => policy.Id == id, cancellationToken);

    public Task<Policy?> FindPolicyBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Policies.AsNoTracking().Include(policy => policy.President).AsQueryable();
        if (publishedOnly)
            query = query.Where(policy => policy.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(policy => policy.Slug == slug, cancellationToken);
    }

    public async Task<PagedResult<Policy>> PagePoliciesAsync(bool publishedOnly, Guid? presidentId, string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (current, size, skip) = Page(page, pageSize);
        var rows = db.Policies.AsNoTracking().Include(policy => policy.President).AsQueryable();
        if (publishedOnly)
            rows = rows.Where(policy => policy.Status == PublicationStatus.Published);
        if (presidentId is { } id)
            rows = rows.Where(policy => policy.PresidentId == id);
        rows = ApplyTerm(rows, term, policy => policy.SearchText);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderBy(policy => policy.Name).Skip(skip).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<Policy>(items, current, size, total);
    }

    public Task<bool> PolicySlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Policies.AnyAsync(policy => policy.Slug == slug && policy.Id != exceptId, cancellationToken);

    public void Add(Policy entity) => db.Policies.Add(entity);
    public void Remove(Policy entity) => db.Policies.Remove(entity);

    public Task<SourceRecord?> FindSourceAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sources.FirstOrDefaultAsync(source => source.Id == id, cancellationToken);

    public Task<SourceRecord?> FindSourceBySlugAsync(string slug, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Sources.AsNoTracking().AsQueryable();
        if (publishedOnly)
            query = query.Where(source => source.Status == PublicationStatus.Published);
        return query.FirstOrDefaultAsync(source => source.Slug == slug, cancellationToken);
    }

    public Task<SourceRecord?> FindSourceByUrlAsync(string url, CancellationToken cancellationToken) =>
        db.Sources.FirstOrDefaultAsync(source => source.Url == url, cancellationToken);

    public async Task<PagedResult<SourceRecord>> PageSourcesAsync(bool publishedOnly, string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (current, size, skip) = Page(page, pageSize);
        var rows = db.Sources.AsNoTracking().AsQueryable();
        if (publishedOnly)
            rows = rows.Where(source => source.Status == PublicationStatus.Published);
        rows = ApplyTerm(rows, term, source => source.SearchText);
        var total = await rows.CountAsync(cancellationToken);
        var items = await rows.OrderBy(source => source.Name).Skip(skip).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<SourceRecord>(items, current, size, total);
    }

    public Task<bool> SourceSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Sources.AnyAsync(source => source.Slug == slug && source.Id != exceptId, cancellationToken);

    public async Task<int> CountSourceUsageAsync(Guid id, CancellationToken cancellationToken)
    {
        var links = await db.FactSources.CountAsync(link => link.SourceId == id, cancellationToken);
        var values = await db.PresidentIndicators.CountAsync(value => value.SourceId == id, cancellationToken);
        var methods = await db.Indicators.CountAsync(indicator => indicator.MethodologySourceId == id, cancellationToken);
        return links + values + methods;
    }

    public void Add(SourceRecord entity) => db.Sources.Add(entity);
    public void Remove(SourceRecord entity) => db.Sources.Remove(entity);

    public Task<Indicator?> FindIndicatorAsync(Guid id, CancellationToken cancellationToken) =>
        db.Indicators.FirstOrDefaultAsync(indicator => indicator.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Indicator>> ListIndicatorsAsync(bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Indicators.AsNoTracking().AsQueryable();
        if (publishedOnly)
            query = query.Where(indicator => indicator.Status == PublicationStatus.Published);
        return await query.OrderBy(indicator => indicator.Name).ToListAsync(cancellationToken);
    }

    public Task<bool> IndicatorSlugExistsAsync(string slug, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Indicators.AnyAsync(indicator => indicator.Slug == slug && indicator.Id != exceptId, cancellationToken);

    public void Add(Indicator entity) => db.Indicators.Add(entity);
    public void Remove(Indicator entity) => db.Indicators.Remove(entity);

    public Task<PresidentIndicator?> FindIndicatorValueAsync(Guid id, CancellationToken cancellationToken) =>
        db.PresidentIndicators.Include(value => value.Indicator).Include(value => value.Source).FirstOrDefaultAsync(value => value.Id == id, cancellationToken);

    public async Task<IReadOnlyList<PresidentIndicator>> ListIndicatorValuesAsync(bool publishedOnly, Guid? presidentId, CancellationToken cancellationToken)
    {
        var query = db.PresidentIndicators.AsNoTracking().Include(value => value.Indicator).Include(value => value.Source).AsQueryable();
        if (publishedOnly)
            query = query.Where(value => value.Status == PublicationStatus.Published);
        if (presidentId is { } id)
            query = query.Where(value => value.PresidentId == id);
        return await query.OrderBy(value => value.Year).ToListAsync(cancellationToken);
    }

    public Task<bool> IndicatorValueExistsAsync(Guid presidentId, Guid indicatorId, int year, Guid? exceptId, CancellationToken cancellationToken) =>
        db.PresidentIndicators.AnyAsync(value => value.PresidentId == presidentId && value.IndicatorId == indicatorId && value.Year == year && value.Id != exceptId, cancellationToken);

    public void Add(PresidentIndicator entity) => db.PresidentIndicators.Add(entity);
    public void Remove(PresidentIndicator entity) => db.PresidentIndicators.Remove(entity);

    public Task<SourcedStatement?> FindStatementAsync(Guid id, CancellationToken cancellationToken) =>
        db.Statements.FirstOrDefaultAsync(statement => statement.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SourcedStatement>> ListStatementsAsync(ContentEntityType? type, Guid? entityId, bool publishedOnly, CancellationToken cancellationToken)
    {
        var query = db.Statements.AsNoTracking().AsQueryable();
        if (publishedOnly)
            query = query.Where(statement => statement.Status == PublicationStatus.Published);
        if (type is { } entityType)
            query = query.Where(statement => statement.EntityType == entityType);
        if (entityId is { } id)
            query = query.Where(statement => statement.EntityId == id);
        return await query.OrderBy(statement => statement.Kind).ToListAsync(cancellationToken);
    }

    public void Add(SourcedStatement entity) => db.Statements.Add(entity);
    public void Remove(SourcedStatement entity) => db.Statements.Remove(entity);

    public async Task<IReadOnlyList<FactSource>> ListFactSourcesAsync(ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken)
    {
        var query = db.FactSources.AsNoTracking().Include(link => link.Source).AsQueryable();
        if (type is { } entityType)
            query = query.Where(link => link.EntityType == entityType);
        if (entityId is { } id)
            query = query.Where(link => link.EntityId == id);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FactSource>> ListFactSourcesForAsync(ContentEntityType type, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken)
    {
        if (entityIds.Count == 0)
            return [];
        return await db.FactSources.AsNoTracking().Include(link => link.Source)
            .Where(link => link.EntityType == type && entityIds.Contains(link.EntityId))
            .ToListAsync(cancellationToken);
    }

    public Task<FactSource?> FindFactSourceAsync(Guid id, CancellationToken cancellationToken) =>
        db.FactSources.FirstOrDefaultAsync(link => link.Id == id, cancellationToken);

    public Task<int> CountPublishedSourcesAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken) =>
        db.FactSources.CountAsync(link => link.EntityType == type && link.EntityId == entityId && link.Source!.Status == PublicationStatus.Published, cancellationToken);

    public void Add(FactSource entity) => db.FactSources.Add(entity);
    public void Remove(FactSource entity) => db.FactSources.Remove(entity);

    public async Task ReplaceCategoriesAsync(ContentEntityType type, Guid entityId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var existing = await db.CategoryLinks.Where(link => link.EntityType == type && link.EntityId == entityId).ToListAsync(cancellationToken);
        db.CategoryLinks.RemoveRange(existing);
        var first = true;
        foreach (var categoryId in categoryIds.Distinct())
        {
            db.CategoryLinks.Add(new ContentCategoryLink
            {
                EntityType = type,
                EntityId = entityId,
                CategoryId = categoryId,
                IsPrimary = first
            });
            first = false;
        }
    }

    public async Task<IReadOnlyList<ContentCategoryLink>> ListCategoryLinksAsync(ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken)
    {
        var query = db.CategoryLinks.AsNoTracking().Include(link => link.Category).AsQueryable();
        if (type is { } entityType)
            query = query.Where(link => link.EntityType == entityType);
        if (entityId is { } id)
            query = query.Where(link => link.EntityId == id);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContentCategoryLink>> ListCategoryLinksForAsync(ContentEntityType type, IReadOnlyCollection<Guid> entityIds, CancellationToken cancellationToken)
    {
        if (entityIds.Count == 0)
            return [];
        return await db.CategoryLinks.AsNoTracking().Include(link => link.Category)
            .Where(link => link.EntityType == type && entityIds.Contains(link.EntityId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SourceDivergence>> ListDivergencesAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken) =>
        await db.Divergences.AsNoTracking().Where(item => item.EntityType == type && item.EntityId == entityId).ToListAsync(cancellationToken);

    public void Add(SourceDivergence entity) => db.Divergences.Add(entity);

    public async Task RemoveOwnedLinksAsync(ContentEntityType type, Guid entityId, CancellationToken cancellationToken)
    {
        var statements = await db.Statements.Where(statement => statement.EntityType == type && statement.EntityId == entityId).ToListAsync(cancellationToken);
        var statementIds = statements.Select(statement => statement.Id).ToList();
        var facts = await db.FactSources.Where(link =>
            (link.EntityType == type && link.EntityId == entityId) ||
            (link.EntityType == ContentEntityType.SourcedStatement && statementIds.Contains(link.EntityId))).ToListAsync(cancellationToken);
        var categories = await db.CategoryLinks.Where(link => link.EntityType == type && link.EntityId == entityId).ToListAsync(cancellationToken);
        var divergences = await db.Divergences.Where(item => item.EntityType == type && item.EntityId == entityId).ToListAsync(cancellationToken);
        db.FactSources.RemoveRange(facts);
        db.Statements.RemoveRange(statements);
        db.CategoryLinks.RemoveRange(categories);
        db.Divergences.RemoveRange(divergences);
    }

    public async Task<IReadOnlyList<IngestionFingerprint>> ListIngestionFingerprintsAsync(string adapter, CancellationToken cancellationToken) =>
        await db.IngestionRecords.AsNoTracking()
            .Where(record => record.AdapterName == adapter)
            .Select(record => new IngestionFingerprint(record.AdapterName, record.ContentHash, record.SourceUrl, record.ExternalId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<IngestionRecord>> ListIngestionAsync(int take, CancellationToken cancellationToken) =>
        await db.IngestionRecords.AsNoTracking().OrderByDescending(record => record.CollectedAt).Take(Math.Clamp(take, 1, 100)).ToListAsync(cancellationToken);

    public void Add(IngestionRecord entity) => db.IngestionRecords.Add(entity);

    public async Task<AdminStatsDto> AdminStatsAsync(CancellationToken cancellationToken)
    {
        var drafts = await db.Presidents.CountAsync(item => item.Status == PublicationStatus.Draft, cancellationToken)
            + await db.HistoricalEvents.CountAsync(item => item.Status == PublicationStatus.Draft, cancellationToken)
            + await db.Laws.CountAsync(item => item.Status == PublicationStatus.Draft, cancellationToken)
            + await db.Policies.CountAsync(item => item.Status == PublicationStatus.Draft, cancellationToken)
            + await db.Sources.CountAsync(item => item.Status == PublicationStatus.Draft, cancellationToken);
        return new AdminStatsDto(
            await db.Presidents.CountAsync(cancellationToken),
            await db.Presidencies.CountAsync(cancellationToken),
            await db.HistoricalEvents.CountAsync(cancellationToken),
            await db.Laws.CountAsync(cancellationToken),
            await db.Policies.CountAsync(cancellationToken),
            await db.Indicators.CountAsync(cancellationToken),
            await db.Categories.CountAsync(cancellationToken),
            await db.Sources.CountAsync(cancellationToken),
            drafts);
    }

    public async Task<PublicCounts> PublicCountsAsync(CancellationToken cancellationToken) => new(
        await db.Presidents.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Presidencies.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.HistoricalEvents.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Laws.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Policies.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Sources.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Indicators.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken),
        await db.Categories.CountAsync(item => item.Status == PublicationStatus.Published, cancellationToken));

    public async Task<IReadOnlyList<SearchHitRow>> SearchRowsAsync(string foldedTerm, string digits, CancellationToken cancellationToken)
    {
        var hits = new List<SearchHitRow>();
        if (foldedTerm.Length >= 2)
        {
            hits.AddRange(await db.Presidents.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "President", item.FullName, item.Party, "/presidentes/" + item.Slug, item.Biography))
                .ToListAsync(cancellationToken));
            hits.AddRange(await db.HistoricalEvents.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "Event", item.Title, item.NeutralSummary, "/acontecimentos/" + item.Slug, item.NeutralSummary))
                .ToListAsync(cancellationToken));
            hits.AddRange(await db.Laws.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && (item.SearchText.Contains(foldedTerm) || (digits.Length >= 3 && item.NumberNormalized.Contains(digits))))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "Law", item.Title, item.Number + "/" + item.Year, "/leis/" + item.Slug, item.Summary))
                .ToListAsync(cancellationToken));
            hits.AddRange(await db.Policies.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "Policy", item.Name, item.Objective, "/politicas/" + item.Slug, item.Description))
                .ToListAsync(cancellationToken));
            hits.AddRange(await db.Sources.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "Source", item.Name, item.Publisher, "/fontes/" + item.Slug, item.Title))
                .ToListAsync(cancellationToken));
        }
        else if (digits.Length >= 3)
        {
            hits.AddRange(await db.Laws.AsNoTracking()
                .Where(item => item.Status == PublicationStatus.Published && item.NumberNormalized.Contains(digits))
                .Take(20)
                .Select(item => new SearchHitRow(item.Id, "Law", item.Title, item.Number + "/" + item.Year, "/leis/" + item.Slug, item.Summary))
                .ToListAsync(cancellationToken));
        }

        return hits.DistinctBy(hit => (hit.Kind, hit.Id)).ToList();
    }

    public async Task<IReadOnlyList<EvidenceItem>> EvidenceAsync(string foldedTerm, int take, CancellationToken cancellationToken)
    {
        if (foldedTerm.Length < 2)
            return [];

        var items = new List<EvidenceItem>();
        await AddEvidenceAsync(items, ContentEntityType.HistoricalEvent, await db.HistoricalEvents.AsNoTracking()
            .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
            .OrderByDescending(item => item.Importance)
            .Take(take)
            .Select(item => new TextRow(item.Id, item.NeutralSummary ?? item.Description ?? item.Title))
            .ToListAsync(cancellationToken), cancellationToken);
        await AddEvidenceAsync(items, ContentEntityType.Law, await db.Laws.AsNoTracking()
            .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
            .Take(take)
            .Select(item => new TextRow(item.Id, item.Summary ?? item.Title))
            .ToListAsync(cancellationToken), cancellationToken);
        await AddEvidenceAsync(items, ContentEntityType.Policy, await db.Policies.AsNoTracking()
            .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
            .Take(take)
            .Select(item => new TextRow(item.Id, item.Description ?? item.Name))
            .ToListAsync(cancellationToken), cancellationToken);
        await AddEvidenceAsync(items, ContentEntityType.SourcedStatement, await db.Statements.AsNoTracking()
            .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
            .Take(take)
            .Select(item => new TextRow(item.Id, item.Text))
            .ToListAsync(cancellationToken), cancellationToken);
        await AddEvidenceAsync(items, ContentEntityType.President, await db.Presidents.AsNoTracking()
            .Where(item => item.Status == PublicationStatus.Published && item.SearchText.Contains(foldedTerm))
            .Take(take)
            .Select(item => new TextRow(item.Id, item.Biography ?? item.FullName))
            .ToListAsync(cancellationToken), cancellationToken);

        return items.Take(take).ToList();
    }

    public Task<bool> EntityExistsAsync(ContentEntityType type, Guid id, CancellationToken cancellationToken) => type switch
    {
        ContentEntityType.President => db.Presidents.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Presidency => db.Presidencies.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.HistoricalEvent => db.HistoricalEvents.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Law => db.Laws.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Policy => db.Policies.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Indicator => db.Indicators.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.PresidentIndicator => db.PresidentIndicators.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.SourcedStatement => db.Statements.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Category => db.Categories.AnyAsync(item => item.Id == id, cancellationToken),
        ContentEntityType.Source => db.Sources.AnyAsync(item => item.Id == id, cancellationToken),
        _ => Task.FromResult(false)
    };

    private async Task AddEvidenceAsync(List<EvidenceItem> items, ContentEntityType type, List<TextRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.Id).ToList();
        if (ids.Count == 0)
            return;

        var links = await db.FactSources.AsNoTracking().Include(link => link.Source)
            .Where(link => link.EntityType == type && ids.Contains(link.EntityId) && link.Source!.Status == PublicationStatus.Published)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Text))
                continue;
            foreach (var link in links.Where(link => link.EntityId == row.Id && link.Source is not null))
            {
                items.Add(new EvidenceItem(
                    link.SourceId,
                    row.Text,
                    link.Source!.Name,
                    link.Source.Url,
                    link.Source.PublicationDate ?? link.Source.AccessedAt,
                    link.Source.ReliabilityLevel,
                    EnumLabels.For(link.Source.ReliabilityLevel)));
            }
        }
    }

    private static (int Page, int Size, int Skip) Page(int page, int pageSize)
    {
        var current = page < 1 ? 1 : page;
        var size = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        return (current, size, (current - 1) * size);
    }

    private static IQueryable<T> ApplyTerm<T>(IQueryable<T> query, string? term, System.Linq.Expressions.Expression<Func<T, string>> search)
    {
        var folded = TextNormalizer.Fold(term);
        if (folded.Length == 0)
            return query;
        var parameter = search.Parameters[0];
        var method = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var body = System.Linq.Expressions.Expression.Call(search.Body, method, System.Linq.Expressions.Expression.Constant(folded));
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private sealed record TextRow(Guid Id, string Text);
}
