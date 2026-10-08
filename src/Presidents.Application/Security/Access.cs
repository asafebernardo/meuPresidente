using Presidents.Domain.Common;
using Presidents.Domain.Enums;
using Presidents.Domain.Labels;
using Presidents.Domain.Rules;

namespace Presidents.Application.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Reviewer = "Reviewer";
    public static readonly IReadOnlyList<string> All = [Admin, Editor, Reviewer];
}

public sealed record ActorContext(string? UserId, IReadOnlyCollection<string> Roles)
{
    public static ActorContext Anonymous { get; } = new(null, []);

    public bool IsAdmin => Has(AppRoles.Admin);
    public bool IsEditor => Has(AppRoles.Editor);
    public bool IsReviewer => Has(AppRoles.Reviewer);
    public bool CanEdit => IsAdmin || IsEditor || IsReviewer;
    public bool CanReview => IsAdmin || IsReviewer;
    public bool CanDelete => IsAdmin;

    public bool Has(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public static class ContentPermissions
{
    public static void EnsureCanEdit(ActorContext actor)
    {
        if (!actor.CanEdit)
            throw new ForbiddenException("Este perfil não pode editar o acervo.");
    }

    public static void EnsureCanDelete(ActorContext actor)
    {
        if (!actor.CanDelete)
            throw new ForbiddenException("Apenas administradores podem excluir registros.");
    }
}

public static class PublicationWorkflow
{
    public static void Ensure(PublicationStatus from, PublicationStatus to, ActorContext actor)
    {
        if (from == to)
            return;

        if (!actor.CanEdit)
            throw new ForbiddenException("É preciso um perfil de edição para alterar a situação do conteúdo.");

        var allowed = (from, to) switch
        {
            (PublicationStatus.Draft, PublicationStatus.Review) => actor.CanEdit,
            (PublicationStatus.Review, PublicationStatus.Draft) => actor.CanReview,
            (PublicationStatus.Review, PublicationStatus.Published) => actor.CanReview,
            (PublicationStatus.Published, PublicationStatus.Draft) => actor.CanReview,
            (PublicationStatus.Published, PublicationStatus.Archived) => actor.CanReview,
            (PublicationStatus.Draft, PublicationStatus.Published) => actor.IsAdmin,
            (PublicationStatus.Draft, PublicationStatus.Archived) => actor.IsAdmin,
            (PublicationStatus.Review, PublicationStatus.Archived) => actor.IsAdmin,
            (PublicationStatus.Archived, PublicationStatus.Draft) => actor.IsAdmin,
            _ => false
        };

        if (!allowed)
            throw new DomainException($"A transição de {EnumLabels.For(from)} para {EnumLabels.For(to)} não é permitida para este perfil.");
    }

    public static void Apply(PublishableEntity entity, PublicationStatus target, ActorContext actor, ContentEntityType type, int publishedSourceCount)
    {
        Ensure(entity.Status, target, actor);
        if (target == PublicationStatus.Published && PublicationRules.RequiresSource(type))
            PublicationRules.EnsureHasSource(publishedSourceCount);

        entity.Status = target;
        entity.PublishedAt = target == PublicationStatus.Published ? DateTimeOffset.UtcNow : null;
    }
}
