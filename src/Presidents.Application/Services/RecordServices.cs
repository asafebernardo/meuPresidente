using FluentValidation;
using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Security;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;

namespace Presidents.Application.Services;

public sealed class CategoryService(IHistoryStore store, IValidator<CategoryForm> validator)
{
    public Task<IReadOnlyList<Category>> ListPublicAsync(CancellationToken cancellationToken) =>
        store.ListCategoriesAsync(true, cancellationToken);

    public async Task<PagedResult<AdminRow>> ListAdminAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var items = await store.ListCategoriesAsync(false, cancellationToken);
        var rows = items.Select(item => new AdminRow(item.Id, item.Name, EnumLabels.For(item.Status), item.Slug, item.Provenance == DataProvenance.DemonstrationSeed)).ToList();
        return new PagedResult<AdminRow>(rows, 1, rows.Count, rows.Count);
    }

    public async Task<CategoryForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var category = await store.FindCategoryAsync(id, cancellationToken);
        return category is null ? null : new CategoryForm { Id = category.Id, Name = category.Name, Slug = category.Slug, Description = category.Description, SortOrder = category.SortOrder };
    }

    public async Task<Guid> SaveAsync(CategoryForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        Category category;
        if (form.Id is { } id)
            category = await store.FindCategoryAsync(id, cancellationToken) ?? throw new NotFoundException("Categoria não encontrada.");
        else
        {
            category = Category.Create(form.Name, form.Description, form.Slug);
            store.Add(category);
        }

        category.Name = form.Name.Trim();
        category.Description = Clean(form.Description);
        category.SortOrder = form.SortOrder;
        category.Slug = await Slugs.UniqueAsync(form.Slug ?? form.Name, slug => store.CategorySlugExistsAsync(slug, category.Id, cancellationToken), cancellationToken);
        category.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var category = await store.FindCategoryAsync(id, cancellationToken) ?? throw new NotFoundException("Categoria não encontrada.");
        if (await store.CountCategoryLinksAsync(id, cancellationToken) > 0)
            throw new DomainException("A categoria está em uso e não pode ser excluída.");
        store.Remove(category);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class EventService(IHistoryStore store, IValidator<EventForm> validator)
{
    public async Task<EventForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var ev = await store.FindEventAsync(id, cancellationToken);
        if (ev is null) return null;
        var links = await store.ListCategoryLinksAsync(ContentEntityType.HistoricalEvent, id, cancellationToken);
        return new EventForm
        {
            Id = ev.Id,
            PresidentId = ev.PresidentId,
            PresidencyId = ev.PresidencyId,
            Title = ev.Title,
            Slug = ev.Slug,
            Description = ev.Description,
            EventDate = ev.EventDate,
            EndDate = ev.EndDate,
            Importance = ev.Importance,
            EventType = ev.EventType,
            NeutralSummary = ev.NeutralSummary,
            SummaryKind = ev.SummaryKind,
            DivergenceNote = ev.DivergenceNote,
            CategoryIds = links.Select(link => link.CategoryId).ToList()
        };
    }

    public async Task<Guid> SaveAsync(EventForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        if (form.PresidentId is { } presidentId && await store.FindPresidentAsync(presidentId, cancellationToken) is null)
            throw new NotFoundException("Presidente não encontrado.");
        await EnsureCategoriesAsync(form.CategoryIds, cancellationToken);

        HistoricalEvent ev;
        if (form.Id is { } id)
            ev = await store.FindEventAsync(id, cancellationToken) ?? throw new NotFoundException("Acontecimento não encontrado.");
        else
        {
            ev = HistoricalEvent.Create(form.Title, form.Slug);
            store.Add(ev);
        }

        ev.PresidentId = form.PresidentId;
        ev.PresidencyId = form.PresidencyId;
        ev.Title = form.Title.Trim();
        ev.Description = Clean(form.Description);
        ev.EventDate = form.EventDate;
        ev.EndDate = form.EndDate;
        ev.Importance = form.Importance;
        ev.EventType = form.EventType;
        ev.NeutralSummary = Clean(form.NeutralSummary);
        ev.SummaryKind = form.SummaryKind;
        ev.DivergenceNote = Clean(form.DivergenceNote);
        ev.EnsureInvariants();
        ev.Slug = await Slugs.UniqueAsync(form.Slug ?? form.Title, slug => store.EventSlugExistsAsync(slug, ev.Id, cancellationToken), cancellationToken);
        ev.RebuildSearchText();
        await store.ReplaceCategoriesAsync(ContentEntityType.HistoricalEvent, ev.Id, form.CategoryIds, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return ev.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var ev = await store.FindEventAsync(id, cancellationToken) ?? throw new NotFoundException("Acontecimento não encontrado.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.HistoricalEvent, id, cancellationToken);
        store.Remove(ev);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureCategoriesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids.Distinct())
        {
            if (await store.FindCategoryAsync(id, cancellationToken) is null)
                throw new DomainException("Uma das categorias informadas não existe.");
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class LawService(IHistoryStore store, IValidator<LawForm> validator)
{
    public async Task<LawForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var law = await store.FindLawAsync(id, cancellationToken);
        if (law is null) return null;
        var links = await store.ListCategoryLinksAsync(ContentEntityType.Law, id, cancellationToken);
        return new LawForm
        {
            Id = law.Id,
            Number = law.Number,
            Year = law.Year,
            Kind = law.Kind,
            Title = law.Title,
            Slug = law.Slug,
            Summary = law.Summary,
            FullTextUrl = law.FullTextUrl,
            PublicationDate = law.PublicationDate,
            SanctionDate = law.SanctionDate,
            EffectiveDate = law.EffectiveDate,
            VetoDate = law.VetoDate,
            PromulgationDate = law.PromulgationDate,
            OperationalStatus = law.OperationalStatus,
            Origin = law.Origin,
            Proposer = law.Proposer,
            AttributeAuthorship = law.AttributedAsAuthor,
            LegislativeProcessNote = law.LegislativeProcessNote,
            VetoNote = law.VetoNote,
            DivergenceNote = law.DivergenceNote,
            PresidentInOfficeId = law.PresidentInOfficeId,
            CategoryIds = links.Select(link => link.CategoryId).ToList()
        };
    }

    public async Task<Guid> SaveAsync(LawForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        if (form.PresidentInOfficeId is { } presidentId && await store.FindPresidentAsync(presidentId, cancellationToken) is null)
            throw new NotFoundException("Presidente em exercício não encontrado.");
        if (await store.LawIdentityExistsAsync(form.Kind, form.Number.Trim(), form.Year, form.Id, cancellationToken))
            throw new DomainException("Já existe um ato com o mesmo tipo, número e ano.");

        Law law;
        if (form.Id is { } id)
            law = await store.FindLawAsync(id, cancellationToken) ?? throw new NotFoundException("Lei não encontrada.");
        else
        {
            law = Law.Create(form.Kind, form.Number, form.Year, form.Title);
            store.Add(law);
        }

        law.Kind = form.Kind;
        law.Number = form.Number.Trim();
        law.NumberNormalized = TextNormalizer.Digits(law.Number);
        law.Year = form.Year;
        law.Title = form.Title.Trim();
        law.Summary = Clean(form.Summary);
        law.FullTextUrl = Clean(form.FullTextUrl);
        law.PublicationDate = form.PublicationDate;
        law.SanctionDate = form.SanctionDate;
        law.EffectiveDate = form.EffectiveDate;
        law.VetoDate = form.VetoDate;
        law.PromulgationDate = form.PromulgationDate;
        law.OperationalStatus = form.OperationalStatus;
        law.Origin = form.Origin;
        law.LegislativeProcessNote = Clean(form.LegislativeProcessNote);
        law.VetoNote = Clean(form.VetoNote);
        law.DivergenceNote = Clean(form.DivergenceNote);
        law.PresidentInOfficeId = form.PresidentInOfficeId;
        law.ClearAuthorship();
        if (form.AttributeAuthorship)
            law.AttributeAuthorship(form.Proposer ?? string.Empty);
        else
            law.Proposer = Clean(form.Proposer);

        law.Slug = await Slugs.UniqueAsync(string.IsNullOrWhiteSpace(form.Slug) ? $"{form.Kind} {form.Number} {form.Year}" : form.Slug, slug => store.LawSlugExistsAsync(slug, law.Id, cancellationToken), cancellationToken);
        law.RebuildSearchText();
        await store.ReplaceCategoriesAsync(ContentEntityType.Law, law.Id, form.CategoryIds, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return law.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var law = await store.FindLawAsync(id, cancellationToken) ?? throw new NotFoundException("Lei não encontrada.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.Law, id, cancellationToken);
        store.Remove(law);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class PolicyService(IHistoryStore store, IValidator<PolicyForm> validator)
{
    public async Task<PolicyForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var policy = await store.FindPolicyAsync(id, cancellationToken);
        if (policy is null) return null;
        var links = await store.ListCategoryLinksAsync(ContentEntityType.Policy, id, cancellationToken);
        return new PolicyForm
        {
            Id = policy.Id,
            PresidentId = policy.PresidentId,
            PresidencyId = policy.PresidencyId,
            Name = policy.Name,
            Slug = policy.Slug,
            Description = policy.Description,
            StartDate = policy.StartDate,
            EndDate = policy.EndDate,
            Objective = policy.Objective,
            CategoryIds = links.Select(link => link.CategoryId).ToList()
        };
    }

    public async Task<Guid> SaveAsync(PolicyForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        Domain.Rules.DateRules.EnsurePeriod(form.StartDate, form.EndDate, "política pública");
        Policy policy;
        if (form.Id is { } id)
            policy = await store.FindPolicyAsync(id, cancellationToken) ?? throw new NotFoundException("Política não encontrada.");
        else
        {
            policy = Policy.Create(form.Name, form.Slug);
            store.Add(policy);
        }

        policy.PresidentId = form.PresidentId;
        policy.PresidencyId = form.PresidencyId;
        policy.Name = form.Name.Trim();
        policy.Description = Clean(form.Description);
        policy.StartDate = form.StartDate;
        policy.EndDate = form.EndDate;
        policy.Objective = Clean(form.Objective);
        policy.Slug = await Slugs.UniqueAsync(form.Slug ?? form.Name, slug => store.PolicySlugExistsAsync(slug, policy.Id, cancellationToken), cancellationToken);
        policy.RebuildSearchText();
        await store.ReplaceCategoriesAsync(ContentEntityType.Policy, policy.Id, form.CategoryIds, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return policy.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var policy = await store.FindPolicyAsync(id, cancellationToken) ?? throw new NotFoundException("Política não encontrada.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.Policy, id, cancellationToken);
        store.Remove(policy);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SourceService(IHistoryStore store, IValidator<SourceForm> validator)
{
    public async Task<SourceForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var source = await store.FindSourceAsync(id, cancellationToken);
        return source is null ? null : new SourceForm
        {
            Id = source.Id,
            Name = source.Name,
            Slug = source.Slug,
            Url = source.Url,
            SourceType = source.SourceType,
            Publisher = source.Publisher,
            PublicationDate = source.PublicationDate,
            Author = source.Author,
            Title = source.Title,
            AccessedAt = source.AccessedAt,
            ReliabilityLevel = source.ReliabilityLevel,
            Notes = source.Notes
        };
    }

    public async Task<Guid> SaveAsync(SourceForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        if (!string.IsNullOrWhiteSpace(form.Url))
        {
            var existing = await store.FindSourceByUrlAsync(form.Url.Trim(), cancellationToken);
            if (existing is not null && existing.Id != form.Id)
                throw new DomainException("Já existe uma fonte com esta URL.");
        }

        SourceRecord source;
        if (form.Id is { } id)
            source = await store.FindSourceAsync(id, cancellationToken) ?? throw new NotFoundException("Fonte não encontrada.");
        else
        {
            source = SourceRecord.Create(form.Name, form.SourceType, form.ReliabilityLevel, form.Url);
            store.Add(source);
        }

        source.Name = form.Name.Trim();
        source.Url = Clean(form.Url);
        source.SourceType = form.SourceType;
        source.Publisher = Clean(form.Publisher);
        source.PublicationDate = form.PublicationDate;
        source.Author = Clean(form.Author);
        source.Title = Clean(form.Title);
        source.AccessedAt = form.AccessedAt;
        source.ReliabilityLevel = form.ReliabilityLevel;
        source.Notes = Clean(form.Notes);
        source.Slug = await Slugs.UniqueAsync(form.Slug ?? form.Name, slug => store.SourceSlugExistsAsync(slug, source.Id, cancellationToken), cancellationToken);
        source.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return source.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var source = await store.FindSourceAsync(id, cancellationToken) ?? throw new NotFoundException("Fonte não encontrada.");
        if (await store.CountSourceUsageAsync(id, cancellationToken) > 0)
            throw new DomainException("A fonte está ligada a registros e não pode ser excluída.");
        store.Remove(source);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class IndicatorService(IHistoryStore store, IValidator<IndicatorForm> indicatorValidator, IValidator<IndicatorValueForm> valueValidator)
{
    public Task<IReadOnlyList<Indicator>> ListPublicAsync(CancellationToken cancellationToken) =>
        store.ListIndicatorsAsync(true, cancellationToken);

    public async Task<IndicatorForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var indicator = await store.FindIndicatorAsync(id, cancellationToken);
        return indicator is null ? null : new IndicatorForm { Id = indicator.Id, Name = indicator.Name, Unit = indicator.Unit, Description = indicator.Description, MethodologySourceId = indicator.MethodologySourceId };
    }

    public async Task<Guid> SaveAsync(IndicatorForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await indicatorValidator.ValidateAndThrowAsync(form, cancellationToken);
        Indicator indicator;
        if (form.Id is { } id)
            indicator = await store.FindIndicatorAsync(id, cancellationToken) ?? throw new NotFoundException("Indicador não encontrado.");
        else
        {
            indicator = Indicator.Create(form.Name, form.Unit, form.Description);
            store.Add(indicator);
        }

        indicator.Name = form.Name.Trim();
        indicator.Unit = form.Unit.Trim();
        indicator.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim();
        indicator.MethodologySourceId = form.MethodologySourceId;
        indicator.Slug = await Slugs.UniqueAsync(form.Name, slug => store.IndicatorSlugExistsAsync(slug, indicator.Id, cancellationToken), cancellationToken);
        indicator.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return indicator.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var indicator = await store.FindIndicatorAsync(id, cancellationToken) ?? throw new NotFoundException("Indicador não encontrado.");
        if ((await store.ListIndicatorValuesAsync(false, null, cancellationToken)).Any(value => value.IndicatorId == id))
            throw new DomainException("O indicador possui valores e não pode ser excluído.");
        store.Remove(indicator);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> SaveValueAsync(IndicatorValueForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await valueValidator.ValidateAndThrowAsync(form, cancellationToken);
        if (await store.IndicatorValueExistsAsync(form.PresidentId, form.IndicatorId, form.Year, form.Id, cancellationToken))
            throw new DomainException("Já existe valor deste indicador para o presidente e o ano informados.");
        if (await store.FindSourceAsync(form.SourceId, cancellationToken) is null)
            throw new NotFoundException("Fonte do indicador não encontrada.");

        PresidentIndicator value;
        if (form.Id is { } id)
            value = await store.FindIndicatorValueAsync(id, cancellationToken) ?? throw new NotFoundException("Valor não encontrado.");
        else
        {
            value = PresidentIndicator.Create(form.PresidentId, form.IndicatorId, form.Year, form.Value, form.SourceId);
            store.Add(value);
        }

        value.PresidentId = form.PresidentId;
        value.IndicatorId = form.IndicatorId;
        value.Year = form.Year;
        value.Value = form.Value;
        value.SourceId = form.SourceId;
        value.Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
        await store.SaveChangesAsync(cancellationToken);
        var link = FactSource.Link(form.SourceId, ContentEntityType.PresidentIndicator, value.Id, null, "Fonte do valor informado.");
        var existing = await store.ListFactSourcesAsync(ContentEntityType.PresidentIndicator, value.Id, cancellationToken);
        if (existing.All(item => item.SourceId != form.SourceId))
            store.Add(link);
        await store.SaveChangesAsync(cancellationToken);
        return value.Id;
    }

    public async Task<IndicatorValueForm?> GetValueFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var value = await store.FindIndicatorValueAsync(id, cancellationToken);
        return value is null
            ? null
            : new IndicatorValueForm
            {
                Id = value.Id,
                PresidentId = value.PresidentId,
                IndicatorId = value.IndicatorId,
                Year = value.Year,
                Value = value.Value,
                SourceId = value.SourceId,
                Note = value.Note
            };
    }

    public async Task DeleteValueAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var value = await store.FindIndicatorValueAsync(id, cancellationToken) ?? throw new NotFoundException("Valor não encontrado.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.PresidentIndicator, id, cancellationToken);
        store.Remove(value);
        await store.SaveChangesAsync(cancellationToken);
    }
}

public sealed class StatementService(IHistoryStore store, IValidator<StatementForm> validator, IValidator<FactSourceForm> linkValidator)
{
    public async Task<IReadOnlyList<SourcedStatement>> ListAsync(ActorContext actor, ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        return await store.ListStatementsAsync(type, entityId, false, cancellationToken);
    }

    public async Task<Guid> SaveAsync(StatementForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        if (!await store.EntityExistsAsync(form.EntityType, form.EntityId, cancellationToken))
            throw new NotFoundException("O registro ligado à afirmação não existe.");

        SourcedStatement statement;
        if (form.Id is { } id)
            statement = await store.FindStatementAsync(id, cancellationToken) ?? throw new NotFoundException("Afirmação não encontrada.");
        else
        {
            statement = SourcedStatement.Create(form.EntityType, form.EntityId, form.Kind, form.Text, form.Attribution);
            store.Add(statement);
        }

        statement.EntityType = form.EntityType;
        statement.EntityId = form.EntityId;
        statement.Kind = form.Kind;
        statement.Text = form.Text.Trim();
        statement.Attribution = string.IsNullOrWhiteSpace(form.Attribution) ? null : form.Attribution.Trim();
        if (form.Kind is StatementKind.Opinion or StatementKind.GroupCriticism or StatementKind.HistoricalInterpretation && statement.Attribution is null)
            throw new DomainException("Opinião, crítica e interpretação histórica exigem atribuição.");
        statement.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return statement.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var statement = await store.FindStatementAsync(id, cancellationToken) ?? throw new NotFoundException("Afirmação não encontrada.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.SourcedStatement, id, cancellationToken);
        store.Remove(statement);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> LinkSourceAsync(FactSourceForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await linkValidator.ValidateAndThrowAsync(form, cancellationToken);
        if (await store.FindSourceAsync(form.SourceId, cancellationToken) is null)
            throw new NotFoundException("Fonte não encontrada.");
        if (!await store.EntityExistsAsync(form.EntityType, form.EntityId, cancellationToken))
            throw new NotFoundException("O registro informado não existe.");

        var link = FactSource.Link(form.SourceId, form.EntityType, form.EntityId, form.Excerpt, form.Annotation);
        store.Add(link);
        await store.SaveChangesAsync(cancellationToken);
        return link.Id;
    }

    public async Task<IReadOnlyList<FactSource>> LinksAsync(ActorContext actor, ContentEntityType? type, Guid? entityId, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        return await store.ListFactSourcesAsync(type, entityId, cancellationToken);
    }

    public async Task UnlinkSourceAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var link = await store.FindFactSourceAsync(id, cancellationToken) ?? throw new NotFoundException("Vínculo de fonte não encontrado.");
        store.Remove(link);
        await store.SaveChangesAsync(cancellationToken);
    }
}
