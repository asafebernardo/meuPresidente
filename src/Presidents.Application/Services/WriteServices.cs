using FluentValidation;
using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Security;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;

namespace Presidents.Application.Services;

public sealed class PresidentService(IHistoryStore store, IValidator<PresidentForm> validator)
{
    public async Task<PagedResult<PresidentListItemDto>> ListPublicAsync(string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        var result = await store.PagePresidentsAsync(true, term, page, pageSize, cancellationToken);
        return new PagedResult<PresidentListItemDto>(result.Items.Select(item => MapList(item, publishedOnly: true)).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<PagedResult<AdminRow>> ListAdminAsync(ActorContext actor, string? term, int page, int pageSize, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var result = await store.PagePresidentsAsync(false, term, page, pageSize, cancellationToken);
        return MapAdmin(result, president => president.FullName, president => president.Slug, president => president.Status, president => president.Provenance);
    }

    public async Task<PresidentForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var president = await store.FindPresidentAsync(id, cancellationToken);
        if (president is null) return null;
        return new PresidentForm
        {
            Id = president.Id,
            Name = president.Name,
            FullName = president.FullName,
            Slug = president.Slug,
            BirthDate = president.BirthDate,
            DeathDate = president.DeathDate,
            Biography = president.Biography,
            HistoricalContext = president.HistoricalContext,
            PhotoUrl = president.PhotoUrl,
            WikipediaUrl = president.WikipediaUrl,
            OfficialBiographyUrl = president.OfficialBiographyUrl,
            StartDate = president.StartDate,
            EndDate = president.EndDate,
            Party = president.Party,
            VicePresident = president.VicePresident
        };
    }

    public async Task<Guid> SaveAsync(PresidentForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        President president;
        if (form.Id is { } id)
        {
            president = await store.FindPresidentAsync(id, cancellationToken) ?? throw new NotFoundException("Presidente não encontrado.");
        }
        else
        {
            president = President.Create(form.Name, form.FullName);
            store.Add(president);
        }

        president.Name = form.Name.Trim();
        president.FullName = form.FullName.Trim();
        president.BirthDate = form.BirthDate;
        president.DeathDate = form.DeathDate;
        president.Biography = Clean(form.Biography);
        president.HistoricalContext = Clean(form.HistoricalContext);
        president.PhotoUrl = Clean(form.PhotoUrl);
        president.WikipediaUrl = Clean(form.WikipediaUrl);
        president.OfficialBiographyUrl = Clean(form.OfficialBiographyUrl);
        president.Slug = await Slugs.UniqueAsync(form.Slug ?? form.FullName, slug => store.PresidentSlugExistsAsync(slug, president.Id, cancellationToken), cancellationToken);

        var mandates = await store.ListPresidenciesAsync(false, president.Id, cancellationToken);
        if (mandates.Count > 0)
            president.RefreshSummary(mandates);
        else
        {
            president.StartDate = form.StartDate;
            president.EndDate = form.EndDate;
            president.Party = Clean(form.Party);
            president.VicePresident = Clean(form.VicePresident);
        }

        president.EnsureInvariants();
        president.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return president.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var president = await store.FindPresidentAsync(id, cancellationToken) ?? throw new NotFoundException("Presidente não encontrado.");
        if (await store.CountPresidentBlockersAsync(id, cancellationToken) > 0)
            throw new DomainException("Este presidente possui acontecimentos, leis ou políticas. Remova ou arquive esses registros antes de excluir.");

        foreach (var mandate in await store.ListPresidenciesAsync(false, id, cancellationToken))
        {
            await store.RemoveOwnedLinksAsync(ContentEntityType.Presidency, mandate.Id, cancellationToken);
            store.Remove(mandate);
        }

        foreach (var value in await store.ListIndicatorValuesAsync(false, id, cancellationToken))
            store.Remove(value);

        await store.RemoveOwnedLinksAsync(ContentEntityType.President, id, cancellationToken);
        store.Remove(president);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NamedOption>> OptionsAsync(bool publishedOnly, ActorContext? actor, CancellationToken cancellationToken)
    {
        if (!publishedOnly)
            ContentPermissions.EnsureCanEdit(actor ?? ActorContext.Anonymous);
        var page = await store.PagePresidentsAsync(publishedOnly, null, 1, 100, cancellationToken);
        return page.Items.OrderBy(item => item.FullName).Select(item => new NamedOption(item.Id, item.FullName)).ToList();
    }

    internal static PresidentListItemDto MapList(President president, bool publishedOnly)
    {
        var mandates = president.Presidencies
            .Where(mandate => !publishedOnly || mandate.Status == PublicationStatus.Published)
            .OrderBy(mandate => mandate.StartDate)
            .ToList();
        var start = mandates.Count > 0 ? mandates[0].StartDate : president.StartDate;
        var end = mandates.Count > 0 ? mandates[^1].EndDate : president.EndDate;
        return new PresidentListItemDto(
            president.Id,
            president.Name,
            president.FullName,
            president.Slug,
            president.PhotoUrl,
            Citations.Mandate(start, end),
            president.Party,
            president.VicePresident,
            president.Provenance == DataProvenance.DemonstrationSeed);
    }

    private static PagedResult<AdminRow> MapAdmin<T>(PagedResult<T> result, Func<T, string> title, Func<T, string> extra, Func<T, PublicationStatus> status, Func<T, DataProvenance> provenance) where T : Entity =>
        new(result.Items.Select(item => new AdminRow(item.Id, title(item), EnumLabels.For(status(item)), extra(item), provenance(item) == DataProvenance.DemonstrationSeed)).ToList(), result.Page, result.PageSize, result.TotalCount);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AdminRow(Guid Id, string Title, string Status, string Extra, bool Demonstration);

public sealed class PresidencyService(IHistoryStore store, IValidator<PresidencyForm> validator)
{
    public async Task<IReadOnlyList<Presidency>> ListAsync(ActorContext actor, Guid? presidentId, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        return await store.ListPresidenciesAsync(false, presidentId, cancellationToken);
    }

    public async Task<PresidencyForm?> GetFormAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var mandate = await store.FindPresidencyAsync(id, cancellationToken);
        if (mandate is null) return null;
        return new PresidencyForm
        {
            Id = mandate.Id,
            PresidentId = mandate.PresidentId,
            Ordinal = mandate.Ordinal,
            StartDate = mandate.StartDate,
            EndDate = mandate.EndDate,
            VicePresident = mandate.VicePresident,
            Party = mandate.Party,
            PoliticalContext = mandate.PoliticalContext,
            GovernmentType = mandate.GovernmentType,
            ArrivalMethod = mandate.ArrivalMethod,
            DivergenceNote = mandate.DivergenceNote
        };
    }

    public async Task<Guid> SaveAsync(PresidencyForm form, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        await validator.ValidateAndThrowAsync(form, cancellationToken);
        var president = await store.FindPresidentAsync(form.PresidentId, cancellationToken) ?? throw new NotFoundException("Presidente não encontrado.");
        Presidency mandate;
        if (form.Id is { } id)
            mandate = await store.FindPresidencyAsync(id, cancellationToken) ?? throw new NotFoundException("Mandato não encontrado.");
        else
        {
            mandate = new Presidency { PresidentId = president.Id };
            store.Add(mandate);
        }

        mandate.PresidentId = president.Id;
        mandate.Ordinal = form.Ordinal;
        mandate.SetPeriod(form.StartDate, form.EndDate);
        mandate.VicePresident = Clean(form.VicePresident);
        mandate.Party = Clean(form.Party);
        mandate.PoliticalContext = Clean(form.PoliticalContext);
        mandate.GovernmentType = form.GovernmentType;
        mandate.ArrivalMethod = form.ArrivalMethod;
        mandate.DivergenceNote = Clean(form.DivergenceNote);
        mandate.RebuildSearchText(president.FullName);

        var mandates = (await store.ListPresidenciesAsync(false, president.Id, cancellationToken)).ToList();
        if (mandates.All(item => item.Id != mandate.Id))
            mandates.Add(mandate);
        president.RefreshSummary(mandates);
        president.EnsureInvariants();
        president.RebuildSearchText();
        await store.SaveChangesAsync(cancellationToken);
        return mandate.Id;
    }

    public async Task DeleteAsync(Guid id, ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanDelete(actor);
        var mandate = await store.FindPresidencyAsync(id, cancellationToken) ?? throw new NotFoundException("Mandato não encontrado.");
        await store.RemoveOwnedLinksAsync(ContentEntityType.Presidency, id, cancellationToken);
        store.Remove(mandate);
        var president = await store.FindPresidentAsync(mandate.PresidentId, cancellationToken);
        if (president is not null)
        {
            var remaining = (await store.ListPresidenciesAsync(false, president.Id, cancellationToken))
                .Where(item => item.Id != id)
                .ToList();
            if (remaining.Count == 0)
            {
                president.StartDate = null;
                president.EndDate = null;
            }
            else
                president.RefreshSummary(remaining);
            president.RebuildSearchText();
        }
        await store.SaveChangesAsync(cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
