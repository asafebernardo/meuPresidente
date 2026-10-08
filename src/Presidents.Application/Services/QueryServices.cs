using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Presidents.Application.Abstractions;
using Presidents.Application.Comparison;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Application.Search;
using Presidents.Application.Security;
using Presidents.Domain.Catalog;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;

namespace Presidents.Application.Services;

public sealed class QueryService(IHistoryStore store, IDistributedCache cache)
{
    private static readonly (string Slug, string Name)[] SectionCatalog =
    [
        ("economia", "Economia"),
        ("educacao", "Educação"),
        ("saude", "Saúde"),
        ("infraestrutura", "Infraestrutura"),
        ("relacoes-internacionais", "Relações internacionais")
    ];

    public async Task<HomePageDto> HomeAsync(CancellationToken cancellationToken)
    {
        const string key = "home:published:v2";
        var cached = await cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cached))
        {
            var restored = JsonSerializer.Deserialize<HomePageDto>(cached);
            if (restored is not null)
                return restored;
        }

        var fresh = await BuildHomeAsync(cancellationToken);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(fresh), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        }, cancellationToken);
        return fresh;
    }

    private async Task<HomePageDto> BuildHomeAsync(CancellationToken cancellationToken)
    {
        var since = MandateCoverage.Redemocratization;
        var presidents = await store.PagePresidentsAsync(true, null, 1, 20, cancellationToken);
        var categories = await store.ListCategoriesAsync(true, cancellationToken);
        var events = await store.PageEventsAsync(new EventQuery { PublishedOnly = true, Page = 1, PageSize = 6 }, cancellationToken);
        var laws = await store.PageLawsAsync(new LawQuery { PublishedOnly = true, From = since, Page = 1, PageSize = 6 }, cancellationToken);
        var mandates = await store.ListPresidenciesAsync(true, null, cancellationToken);
        var counts = await store.PublicCountsAsync(cancellationToken);
        var topics = await store.CountLawTopicsAsync(since, null, cancellationToken);
        var lawTotal = await store.CountLawsAsync(since, null, cancellationToken);
        var byPresident = await store.CountLawsByPresidentAsync(since, cancellationToken);
        var areas = await BuildAreasAsync(null, cancellationToken);
        var eventCards = await MapEventsAsync(events.Items, publishedOnly: true, cancellationToken);
        var lawCards = await MapLawsAsync(laws.Items, publishedOnly: true, cancellationToken);
        var shareMap = byPresident.ToDictionary(item => item.PresidentId, item => item.Count);
        var shares = presidents.Items
            .Where(president => president.StartDate >= since)
            .Select(president => new PresidentLawShareDto(
                president.Id,
                president.FullName,
                president.Slug,
                PresidentService.MapList(president, true).Mandate,
                shareMap.GetValueOrDefault(president.Id)))
            .ToList();
        return new HomePageDto(
            presidents.Items.Select(item => PresidentService.MapList(item, true)).ToList(),
            categories.Select(MapCategory).ToList(),
            eventCards,
            lawCards,
            mandates.Where(mandate => mandate.President is { Status: PublicationStatus.Published })
                .OrderBy(mandate => mandate.StartDate)
                .Select(MapMandate)
                .ToList(),
            counts,
            lawTotal,
            topics,
            shares,
            areas);
    }

    public async Task<PresidentDetailDto?> PresidentAsync(string slug, CancellationToken cancellationToken)
    {
        var president = await store.FindPresidentBySlugAsync(slug, true, cancellationToken);
        if (president is null) return null;

        var mandates = president.Presidencies.Where(item => item.Status == PublicationStatus.Published).OrderBy(item => item.StartDate).ToList();
        var events = await store.PageEventsAsync(new EventQuery { PublishedOnly = true, PresidentId = president.Id, Page = 1, PageSize = 100 }, cancellationToken);
        var laws = await store.PageLawsAsync(new LawQuery { PublishedOnly = true, PresidentId = president.Id, From = MandateCoverage.Redemocratization, Page = 1, PageSize = 12 }, cancellationToken);
        var policies = await store.PagePoliciesAsync(true, president.Id, null, 1, 100, cancellationToken);
        var eventCards = await MapEventsAsync(events.Items, true, cancellationToken);
        var lawCards = await MapLawsAsync(laws.Items, true, cancellationToken);
        var policyCards = await MapPoliciesAsync(policies.Items, true, cancellationToken);
        var statements = await MapStatementsAsync(ContentEntityType.President, president.Id, true, cancellationToken);
        var values = await store.ListIndicatorValuesAsync(true, president.Id, cancellationToken);
        var links = await store.ListFactSourcesAsync(ContentEntityType.President, president.Id, cancellationToken);
        var mandateLinks = await store.ListFactSourcesForAsync(ContentEntityType.Presidency, mandates.Select(item => item.Id).ToList(), cancellationToken);
        var topics = await store.CountLawTopicsAsync(MandateCoverage.Redemocratization, president.Id, cancellationToken);
        var lawTotal = await store.CountLawsAsync(MandateCoverage.Redemocratization, president.Id, cancellationToken);
        var periodMetrics = (await BuildAreasAsync(president.Id, cancellationToken)).SelectMany(area => area.Rows).ToList();
        var parties = mandates.Select(item => item.Party).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
        var mandateLabel = mandates.Count == 0
            ? Citations.Mandate(president.StartDate, president.EndDate)
            : string.Join(" · ", mandates.Select(item => Citations.Mandate(item.StartDate, item.EndDate)));

        return new PresidentDetailDto(
            president.Id,
            president.Name,
            president.FullName,
            president.Slug,
            president.PhotoUrl,
            mandateLabel,
            parties.Count > 1 ? "Partidos nos mandatos publicados" : "Partido",
            parties.Count > 0 ? string.Join(", ", parties) : president.Party,
            mandates.Count > 0 ? string.Join("; ", mandates.Select(item => item.VicePresident).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct()) : president.VicePresident,
            president.BirthDate,
            president.DeathDate,
            president.Biography,
            president.HistoricalContext,
            president.WikipediaUrl,
            president.OfficialBiographyUrl,
            president.Provenance == DataProvenance.DemonstrationSeed,
            EnumLabels.For(president.Provenance),
            mandates.Select(mandate => MapPresidency(mandate, mandateLinks)).ToList(),
            eventCards,
            lawCards,
            policyCards,
            statements,
            values.Select(MapPoint).ToList(),
            Citations.Map(links, true),
            BuildSections(eventCards, policyCards),
            topics,
            lawTotal,
            periodMetrics);
    }

    public Task<IReadOnlyList<AreaPanelDto>> AreasAsync(CancellationToken cancellationToken) =>
        BuildAreasAsync(null, cancellationToken);

    public async Task<PagedResult<LawCardDto>> LawsAsync(LawQuery query, CancellationToken cancellationToken)
    {
        query.PublishedOnly = true;
        var page = await store.PageLawsAsync(query, cancellationToken);
        var cards = await MapLawsAsync(page.Items, true, cancellationToken);
        return new PagedResult<LawCardDto>(cards, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<LawCardDto?> LawAsync(string slug, CancellationToken cancellationToken)
    {
        var law = await store.FindLawBySlugAsync(slug, true, cancellationToken);
        if (law is null) return null;
        var cards = await MapLawsAsync([law], true, cancellationToken);
        return cards.FirstOrDefault();
    }

    public async Task<PagedResult<EventCardDto>> EventsAsync(EventQuery query, CancellationToken cancellationToken)
    {
        query.PublishedOnly = true;
        var page = await store.PageEventsAsync(query, cancellationToken);
        var cards = await MapEventsAsync(page.Items, true, cancellationToken);
        return new PagedResult<EventCardDto>(cards, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<EventCardDto?> EventAsync(string slug, CancellationToken cancellationToken)
    {
        var ev = await store.FindEventBySlugAsync(slug, true, cancellationToken);
        if (ev is null) return null;
        return (await MapEventsAsync([ev], true, cancellationToken)).FirstOrDefault();
    }

    public async Task<PolicyCardDto?> PolicyAsync(string slug, CancellationToken cancellationToken)
    {
        var policy = await store.FindPolicyBySlugAsync(slug, true, cancellationToken);
        if (policy is null) return null;
        return (await MapPoliciesAsync([policy], true, cancellationToken)).FirstOrDefault();
    }

    public async Task<TimelineDto> TimelineAsync(CancellationToken cancellationToken)
    {
        var mandates = await store.ListPresidenciesAsync(true, null, cancellationToken);
        var events = await store.PageEventsAsync(new EventQuery { PublishedOnly = true, Page = 1, PageSize = 100 }, cancellationToken);
        return new TimelineDto(
            mandates.Where(mandate => mandate.President is { Status: PublicationStatus.Published })
                .OrderBy(mandate => mandate.StartDate)
                .Select(MapMandate)
                .ToList(),
            events.Items.OrderBy(item => item.EventDate).Select(item => new TimelineEventDto(
                item.Title,
                item.Slug,
                item.EventDate,
                EnumLabels.For(item.Importance),
                item.President?.FullName)).ToList());
    }

    public async Task<SearchResponse> SearchAsync(string? term, CancellationToken cancellationToken)
    {
        var query = term?.Trim() ?? string.Empty;
        var folded = TextNormalizer.Fold(query);
        var digits = TextNormalizer.Digits(query);
        if (folded.Length < 2 && digits.Length < 3)
            return SearchGrouper.Group(query, []);

        var rows = await store.SearchRowsAsync(folded, digits, cancellationToken);
        var hits = rows.Select(row => new SearchHit(
            row.Id,
            Enum.Parse<SearchHitKind>(row.Kind),
            row.Title,
            row.Subtitle,
            row.Slug,
            row.Excerpt)).ToList();
        return SearchGrouper.Group(query, hits);
    }

    public async Task<ComparisonResult> CompareAsync(IReadOnlyList<string> slugs, CancellationToken cancellationToken)
    {
        var selected = slugs.Where(slug => !string.IsNullOrWhiteSpace(slug)).Select(slug => slug.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
        if (selected.Count > 4)
            throw new DomainException("A comparação aceita no máximo 4 presidentes.");

        var slices = new List<PresidentSlice>();
        foreach (var slug in selected)
        {
            var president = await store.FindPresidentBySlugAsync(slug, true, cancellationToken);
            if (president is null) continue;
            var mandates = president.Presidencies.Where(item => item.Status == PublicationStatus.Published).OrderBy(item => item.StartDate).ToList();
            var laws = await store.PageLawsAsync(new LawQuery { PublishedOnly = true, PresidentId = president.Id, Page = 1, PageSize = 1 }, cancellationToken);
            var events = await store.PageEventsAsync(new EventQuery { PublishedOnly = true, PresidentId = president.Id, Page = 1, PageSize = 1 }, cancellationToken);
            var policies = await store.PagePoliciesAsync(true, president.Id, null, 1, 1, cancellationToken);
            var values = await store.ListIndicatorValuesAsync(true, president.Id, cancellationToken);
            var slice = new PresidentSlice
            {
                Id = president.Id,
                Name = president.FullName,
                Slug = president.Slug,
                Party = string.Join(", ", mandates.Select(item => item.Party).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct()),
                VicePresident = string.Join("; ", mandates.Select(item => item.VicePresident).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct()),
                Mandate = mandates.Count == 0 ? "Sem mandato publicado" : string.Join(" · ", mandates.Select(item => Citations.Mandate(item.StartDate, item.EndDate))),
                LawCount = laws.TotalCount,
                EventCount = events.TotalCount,
                PolicyCount = policies.TotalCount
            };
            if (string.IsNullOrWhiteSpace(slice.Party))
                slice.Party = president.Party;
            foreach (var group in values.GroupBy(value => value.Indicator?.Name ?? "Indicador"))
            {
                slice.IndicatorValues[group.Key] = group.Select(value => (value.Year, value.Value, value.Indicator?.Unit ?? string.Empty)).ToList();
            }
            slices.Add(slice);
        }

        return ComparisonAssembler.Assemble(slices);
    }

    public async Task<(CategoryDto? Category, IReadOnlyList<EventCardDto> Events, IReadOnlyList<LawCardDto> Laws, IReadOnlyList<PolicyCardDto> Policies)> CategoryAsync(string slug, CancellationToken cancellationToken)
    {
        var category = await store.FindCategoryBySlugAsync(slug, true, cancellationToken);
        if (category is null) return (null, [], [], []);
        var events = await store.PageEventsAsync(new EventQuery { PublishedOnly = true, CategoryId = category.Id, Page = 1, PageSize = 100 }, cancellationToken);
        var laws = await store.PageLawsAsync(new LawQuery { PublishedOnly = true, CategoryId = category.Id, Page = 1, PageSize = 100 }, cancellationToken);
        var policyLinks = await store.ListCategoryLinksAsync(ContentEntityType.Policy, null, cancellationToken);
        var policies = new List<Policy>();
        foreach (var link in policyLinks.Where(item => item.CategoryId == category.Id))
        {
            var policy = await store.FindPolicyAsync(link.EntityId, cancellationToken);
            if (policy is { Status: PublicationStatus.Published })
                policies.Add(policy);
        }

        return (MapCategory(category), await MapEventsAsync(events.Items, true, cancellationToken), await MapLawsAsync(laws.Items, true, cancellationToken), await MapPoliciesAsync(policies, true, cancellationToken));
    }

    public async Task<SourcePageDto?> SourceAsync(string slug, CancellationToken cancellationToken)
    {
        var source = await store.FindSourceBySlugAsync(slug, true, cancellationToken);
        if (source is null)
            return null;
        return new SourcePageDto(
            source.Id,
            source.Name,
            source.Slug,
            source.Url,
            EnumLabels.For(source.SourceType),
            EnumLabels.For(source.ReliabilityLevel),
            source.Publisher,
            source.Author,
            source.Title,
            source.PublicationDate,
            source.AccessedAt,
            source.Notes,
            source.Provenance == DataProvenance.DemonstrationSeed);
    }

    public async Task<AdminStatsDto> AdminStatsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        return await store.AdminStatsAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AreaPanelDto>> BuildAreasAsync(Guid? presidentId, CancellationToken cancellationToken)
    {
        var mandates = (await store.ListPresidenciesAsync(true, null, cancellationToken))
            .Where(mandate => mandate.President is { Status: PublicationStatus.Published })
            .ToList();
        var observations = await store.ListObservationsAsync(cancellationToken);
        var bySlug = observations.GroupBy(item => item.Indicator?.Slug ?? string.Empty).ToDictionary(group => group.Key, group => group.OrderBy(item => item.ReferenceDate).ToList());
        var visible = mandates.Where(mandate => presidentId is null
            ? mandate.StartDate >= MandateCoverage.Redemocratization || mandate.EndDate is null || mandate.EndDate >= MandateCoverage.Redemocratization
            : mandate.PresidentId == presidentId).ToList();

        MandateMetricDto? Row(Presidency mandate, string indicator, string text, IndicatorObservation? sample)
        {
            if (string.IsNullOrWhiteSpace(text) || mandate.President is null || sample?.Source is null)
                return null;
            return new MandateMetricDto(
                mandate.President.FullName,
                mandate.President.Slug,
                Citations.Mandate(mandate.StartDate, mandate.EndDate),
                indicator,
                text,
                sample.Source.Name,
                sample.Source.Url);
        }

        List<MandateMetricDto> Collect(string slug, Func<IReadOnlyList<IndicatorObservation>, Presidency, (string Text, IndicatorObservation? Sample)> build)
        {
            if (!bySlug.TryGetValue(slug, out var rows))
                return [];
            var result = new List<MandateMetricDto>();
            foreach (var mandate in visible)
            {
                var (text, sample) = build(rows, mandate);
                var row = Row(mandate, rows[0].Indicator?.Name ?? slug, text, sample);
                if (row is not null)
                    result.Add(row);
            }
            return result;
        }

        var inflation = Collect("inflacao", (rows, mandate) =>
        {
            var inside = rows.Where(item => MandateCoverage.Find(mandates, item.ReferenceDate)?.Id == mandate.Id).ToList();
            return (PeriodMetrics.AccumulatedInflation(inside.Select(item => item.Value).ToList()), inside.FirstOrDefault());
        });
        var product = Collect("pib", (rows, mandate) =>
        {
            var inside = rows.Where(item => MandateCoverage.CoversFullYear(mandates, mandate, item.ReferenceDate.Year)).ToList();
            return (PeriodMetrics.AverageAnnualChange(inside.Select(item => item.Value).ToList(), "1996–2023"), inside.FirstOrDefault());
        });
        var jobs = Collect("desemprego", (rows, mandate) =>
        {
            var inside = rows.Where(item => MandateCoverage.Find(mandates, item.ReferenceDate)?.Id == mandate.Id).ToList();
            if (inside.Count == 0)
                return ("", null);
            return (PeriodMetrics.UnemploymentSpan(inside[0].Value, inside[^1].Value), inside[0]);
        });
        var wages = Collect("salario-minimo", (rows, mandate) =>
        {
            var inside = rows.Where(item => MandateCoverage.Find(mandates, item.ReferenceDate)?.Id == mandate.Id).ToList();
            if (inside.Count == 0)
                return ("", null);
            var first = inside[0];
            var last = inside[^1];
            return (PeriodMetrics.NominalWage(first.ReferenceDate, first.Value, last.ReferenceDate, last.Value), first);
        });

        return
        [
            new AreaPanelDto("Economia", "economia", "IPCA mensal (IBGE) e variação anual do volume do PIB (IBGE). " + PeriodMetrics.Disclaimer, inflation.Concat(product).ToList()),
            new AreaPanelDto("Trabalho", "trabalho", "Salário mínimo nominal (Banco Central, SGS 1619) e taxa de desocupação trimestral (IBGE, a partir de 2012). " + PeriodMetrics.Disclaimer, wages.Concat(jobs).ToList())
        ];
    }

    private async Task<IReadOnlyList<EventCardDto>> MapEventsAsync(IReadOnlyList<HistoricalEvent> events, bool publishedOnly, CancellationToken cancellationToken)
    {
        var ids = events.Select(item => item.Id).ToList();
        var links = await store.ListFactSourcesForAsync(ContentEntityType.HistoricalEvent, ids, cancellationToken);
        var categories = await store.ListCategoryLinksForAsync(ContentEntityType.HistoricalEvent, ids, cancellationToken);
        return events.Select(ev => new EventCardDto(
            ev.Id,
            ev.Title,
            ev.Slug,
            ev.EventDate,
            ev.EndDate,
            EnumLabels.For(ev.EventType),
            EnumLabels.For(ev.Importance),
            EnumLabels.For(ev.SummaryKind),
            ev.NeutralSummary,
            ev.Description,
            ev.DivergenceNote,
            ev.President?.FullName,
            ev.President?.Slug,
            Citations.Categories(categories, ContentEntityType.HistoricalEvent, ev.Id),
            ev.Provenance == DataProvenance.DemonstrationSeed,
            Citations.Map(links.Where(link => link.EntityId == ev.Id), publishedOnly))).ToList();
    }

    private async Task<IReadOnlyList<LawCardDto>> MapLawsAsync(IReadOnlyList<Law> laws, bool publishedOnly, CancellationToken cancellationToken)
    {
        var ids = laws.Select(item => item.Id).ToList();
        var links = await store.ListFactSourcesForAsync(ContentEntityType.Law, ids, cancellationToken);
        var categories = await store.ListCategoryLinksForAsync(ContentEntityType.Law, ids, cancellationToken);
        return laws.Select(law => new LawCardDto(
            law.Id,
            law.Number,
            law.Year,
            EnumLabels.For(law.Kind),
            law.Slug,
            law.Title,
            law.Summary,
            EnumLabels.For(law.OperationalStatus),
            EnumLabels.For(law.Origin),
            law.SanctionDate,
            law.PublicationDate,
            law.EffectiveDate,
            law.PromulgationDate,
            law.VetoDate,
            law.FullTextUrl,
            law.Proposer,
            law.AttributedAsAuthor,
            law.PresidentInOffice?.FullName,
            law.PresidentInOffice?.Slug,
            law.LegislativeProcessNote,
            law.VetoNote,
            law.DivergenceNote,
            Citations.Categories(categories, ContentEntityType.Law, law.Id),
            law.Provenance == DataProvenance.DemonstrationSeed,
            Citations.Map(links.Where(link => link.EntityId == law.Id), publishedOnly))).ToList();
    }

    private async Task<IReadOnlyList<PolicyCardDto>> MapPoliciesAsync(IReadOnlyList<Policy> policies, bool publishedOnly, CancellationToken cancellationToken)
    {
        var ids = policies.Select(item => item.Id).ToList();
        var links = await store.ListFactSourcesForAsync(ContentEntityType.Policy, ids, cancellationToken);
        var categories = await store.ListCategoryLinksForAsync(ContentEntityType.Policy, ids, cancellationToken);
        var cards = new List<PolicyCardDto>();
        foreach (var policy in policies)
        {
            var statements = await MapStatementsAsync(ContentEntityType.Policy, policy.Id, publishedOnly, cancellationToken);
            cards.Add(new PolicyCardDto(
                policy.Id,
                policy.Name,
                policy.Slug,
                policy.Description,
                policy.Objective,
                policy.StartDate,
                policy.EndDate,
                policy.President?.FullName,
                policy.President?.Slug,
                Citations.Categories(categories, ContentEntityType.Policy, policy.Id),
                statements.Where(item => item.KindCode is StatementKind.DocumentedFact or StatementKind.OfficialData).ToList(),
                statements.Where(item => item.KindCode is StatementKind.GroupCriticism).ToList(),
                statements.Where(item => item.KindCode is StatementKind.HistoricalInterpretation or StatementKind.Opinion or StatementKind.JournalisticInformation).ToList(),
                policy.Provenance == DataProvenance.DemonstrationSeed,
                Citations.Map(links.Where(link => link.EntityId == policy.Id), publishedOnly)));
        }
        return cards;
    }

    private async Task<IReadOnlyList<StatementDto>> MapStatementsAsync(ContentEntityType type, Guid id, bool publishedOnly, CancellationToken cancellationToken)
    {
        var statements = await store.ListStatementsAsync(type, id, publishedOnly, cancellationToken);
        var links = await store.ListFactSourcesForAsync(ContentEntityType.SourcedStatement, statements.Select(item => item.Id).ToList(), cancellationToken);
        return statements.Select(statement => new StatementDto(
            statement.Id,
            EnumLabels.For(statement.Kind),
            statement.Kind,
            statement.Text,
            statement.Attribution,
            Citations.Map(links.Where(link => link.EntityId == statement.Id), publishedOnly))).ToList();
    }

    private static PresidencyDto MapPresidency(Presidency mandate, IReadOnlyList<FactSource> links) => new(
        mandate.Id,
        mandate.Ordinal,
        mandate.StartDate,
        mandate.EndDate,
        mandate.VicePresident,
        mandate.Party,
        EnumLabels.For(mandate.GovernmentType),
        EnumLabels.For(mandate.ArrivalMethod),
        mandate.PoliticalContext,
        mandate.DivergenceNote,
        mandate.Provenance == DataProvenance.DemonstrationSeed,
        Citations.Map(links.Where(link => link.EntityId == mandate.Id), true));

    private static TimelineMandateDto MapMandate(Presidency mandate) => new(
        mandate.PresidentId,
        mandate.President?.FullName ?? "Presidente",
        mandate.President?.Slug ?? string.Empty,
        mandate.Party,
        mandate.StartDate,
        mandate.EndDate,
        EnumLabels.For(mandate.GovernmentType),
        EnumLabels.For(mandate.ArrivalMethod),
        mandate.Ordinal);

    private static CategoryDto MapCategory(Category category) =>
        new(category.Id, category.Name, category.Slug, category.Description);

    private static IndicatorPointDto MapPoint(PresidentIndicator value) => new(
        value.Id,
        value.Indicator?.Name ?? "Indicador",
        value.Indicator?.Unit ?? string.Empty,
        value.Year,
        value.Value,
        value.Note,
        value.Source is null ? null : new SourceCitationDto(value.Id, value.SourceId, value.Source.Name, value.Source.Url, value.Source.Publisher, value.Source.Author, value.Source.Title, value.Source.PublicationDate, value.Source.AccessedAt, EnumLabels.For(value.Source.SourceType), EnumLabels.For(value.Source.ReliabilityLevel), null, value.Note));

    private static IReadOnlyList<CategorySectionDto> BuildSections(IReadOnlyList<EventCardDto> events, IReadOnlyList<PolicyCardDto> policies) =>
        SectionCatalog.Select(section => new CategorySectionDto(
            section.Slug,
            section.Name,
            events.Where(item => item.Categories.Any(category => TextNormalizer.Slugify(category) == section.Slug)).ToList(),
            policies.Where(item => item.Categories.Any(category => TextNormalizer.Slugify(category) == section.Slug)).ToList())).ToList();
}

public sealed class HistoricalAnswerService(IHistoryStore store, ILlmClient llm, Microsoft.Extensions.Logging.ILogger<HistoricalAnswerService> logger) : IHistoricalAnswerService
{
    public async Task<HistoricalAnswer> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var trimmed = question.Trim();
        if (trimmed.Length < 3)
            return new HistoricalAnswer(HistoricalAnswerComposer.InsufficientEvidence, true, []);

        var pack = new EvidencePack(await store.EvidenceAsync(TextNormalizer.Fold(trimmed), 8, cancellationToken));
        if (pack.Items.Count == 0)
            return HistoricalAnswerComposer.Compose(pack, null);

        IReadOnlyCollection<Guid>? selection = null;
        if (llm.IsEnabled)
        {
            try
            {
                selection = await llm.SelectSourcesAsync(trimmed, pack, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "O modelo de linguagem falhou. A resposta permanece restrita aos trechos recuperados.");
            }
        }

        return HistoricalAnswerComposer.Compose(pack, selection);
    }
}

public sealed class ImportCoordinator(IEnumerable<IDataImporter> importers, IValidator<ImportRequest> validator)
{
    public async Task<ImportResultDto> ImportAsync(ImportChannel channel, ImportRequest request, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(request, cancellationToken);
        var importer = importers.FirstOrDefault(item => item.Channel == channel)
            ?? throw new DomainException("Não há adaptador configurado para este canal de importação.");
        return await importer.ImportAsync(request, cancellationToken);
    }
}
