using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Security;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;

namespace Presidents.Application.Services;

public sealed class AdminCatalogService(IHistoryStore store)
{
    public async Task<IReadOnlyList<AdminRow>> EventsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var page = await store.PageEventsAsync(new EventQuery { PublishedOnly = false, Page = 1, PageSize = 100 }, cancellationToken);
        return page.Items.Select(item => Row(item.Id, item.Title, item.Status, item.Slug, item.Provenance)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> LawsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var page = await store.PageLawsAsync(new LawQuery { PublishedOnly = false, Page = 1, PageSize = 100 }, cancellationToken);
        return page.Items.Select(item => Row(item.Id, item.Title, item.Status, $"{item.Number}/{item.Year}", item.Provenance)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> PoliciesAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var page = await store.PagePoliciesAsync(false, null, null, 1, 100, cancellationToken);
        return page.Items.Select(item => Row(item.Id, item.Name, item.Status, item.Slug, item.Provenance)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> SourcesAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var page = await store.PageSourcesAsync(false, null, 1, 100, cancellationToken);
        return page.Items.Select(item => Row(item.Id, item.Name, item.Status, item.Url ?? item.Slug, item.Provenance)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> IndicatorsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var items = await store.ListIndicatorsAsync(false, cancellationToken);
        return items.Select(item => Row(item.Id, item.Name, item.Status, item.Unit, item.Provenance)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> ValuesAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var items = await store.ListIndicatorValuesAsync(false, null, cancellationToken);
        return items.Select(item => new AdminRow(item.Id, $"{item.Indicator?.Name} · {item.Year}", EnumLabels.For(item.Status), item.Value.ToString(), item.Provenance == DataProvenance.DemonstrationSeed)).ToList();
    }

    public async Task<IReadOnlyList<AdminRow>> StatementsAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        ContentPermissions.EnsureCanEdit(actor);
        var items = await store.ListStatementsAsync(null, null, false, cancellationToken);
        return items.Select(item => Row(item.Id, item.Text, item.Status, EnumLabels.For(item.Kind), item.Provenance)).ToList();
    }

    private static AdminRow Row(Guid id, string title, PublicationStatus status, string extra, DataProvenance provenance) =>
        new(id, title, EnumLabels.For(status), extra, provenance == DataProvenance.DemonstrationSeed);
}
