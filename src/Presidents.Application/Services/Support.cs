using Presidents.Application.Abstractions;
using Presidents.Application.Contracts;
using Presidents.Application.Security;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;

namespace Presidents.Application.Services;

internal static class Pages
{
    public static (int Page, int Size, int Skip) Of(int page, int pageSize)
    {
        var current = page < 1 ? 1 : page;
        var size = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);
        return (current, size, (current - 1) * size);
    }
}

internal static class Slugs
{
    public static async Task<string> UniqueAsync(string preferred, Func<string, Task<bool>> exists, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var slug = TextNormalizer.Slugify(preferred);
        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Não foi possível gerar o endereço amigável.");

        var candidate = slug;
        var suffix = 2;
        while (await exists(candidate))
        {
            candidate = $"{slug}-{suffix}";
            suffix++;
            if (suffix > 50)
                throw new DomainException("Não foi possível gerar um endereço amigável único.");
        }

        return candidate;
    }
}

internal static class Citations
{
    public static IReadOnlyList<SourceCitationDto> Map(IEnumerable<FactSource> links, bool publishedOnly) =>
        links.Where(link => link.Source is not null && (!publishedOnly || link.Source.Status == PublicationStatus.Published))
            .Select(link => new SourceCitationDto(
                link.Id,
                link.SourceId,
                link.Source!.Name,
                link.Source.Url,
                link.Source.Publisher,
                link.Source.Author,
                link.Source.Title,
                link.Source.PublicationDate,
                link.Source.AccessedAt,
                EnumLabels.For(link.Source.SourceType),
                EnumLabels.For(link.Source.ReliabilityLevel),
                link.Excerpt,
                link.Annotation))
            .ToList();

    public static IReadOnlyList<string> Categories(IEnumerable<ContentCategoryLink> links, ContentEntityType type, Guid id) =>
        links.Where(link => link.EntityType == type && link.EntityId == id && link.Category is not null)
            .OrderByDescending(link => link.IsPrimary)
            .ThenBy(link => link.Category!.Name)
            .Select(link => link.Category!.Name)
            .ToList();

    public static string Mandate(DateOnly? start, DateOnly? end)
    {
        if (start is null)
            return "Mandato não informado";
        var finish = end?.ToString("dd/MM/yyyy") ?? "em aberto";
        return $"{start:dd/MM/yyyy} – {finish}";
    }
}

public sealed class PublicationService(IHistoryStore store)
{
    public Task ChangePresidentAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.President, cancellationToken);

    public Task ChangePresidencyAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Presidency, cancellationToken);

    public Task ChangeEventAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.HistoricalEvent, cancellationToken);

    public Task ChangeLawAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Law, cancellationToken);

    public Task ChangePolicyAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Policy, cancellationToken);

    public Task ChangeSourceAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Source, cancellationToken);

    public Task ChangeCategoryAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Category, cancellationToken);

    public Task ChangeIndicatorAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.Indicator, cancellationToken);

    public Task ChangeIndicatorValueAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.PresidentIndicator, cancellationToken);

    public Task ChangeStatementAsync(Guid id, PublicationStatus status, ActorContext actor, CancellationToken cancellationToken) =>
        ChangeAsync(id, status, actor, ContentEntityType.SourcedStatement, cancellationToken);

    private async Task ChangeAsync(Guid id, PublicationStatus status, ActorContext actor, ContentEntityType type, CancellationToken cancellationToken)
    {
        PublishableEntity entity = type switch
        {
            ContentEntityType.President => await store.FindPresidentAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Presidency => await store.FindPresidencyAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.HistoricalEvent => await store.FindEventAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Law => await store.FindLawAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Policy => await store.FindPolicyAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Source => await store.FindSourceAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Category => await store.FindCategoryAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.Indicator => await store.FindIndicatorAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.PresidentIndicator => await store.FindIndicatorValueAsync(id, cancellationToken) ?? throw Missing(),
            ContentEntityType.SourcedStatement => await store.FindStatementAsync(id, cancellationToken) ?? throw Missing(),
            _ => throw new DomainException("Tipo de conteúdo sem fluxo de publicação.")
        };

        var sources = await store.CountPublishedSourcesAsync(type, id, cancellationToken);
        PublicationWorkflow.Apply(entity, status, actor, type, sources);
        await store.SaveChangesAsync(cancellationToken);
    }

    private static NotFoundException Missing() => new("Registro não encontrado.");
}
