using Presidents.Domain.Common;
using Presidents.Domain.Enums;

namespace Presidents.Domain.Rules;

public static class DateRules
{
    public static readonly DateOnly EarliestBirth = new(1700, 1, 1);
    public static readonly DateOnly EarliestMandate = new(1889, 11, 15);

    public static void EnsureBirthDeath(DateOnly? birth, DateOnly? death)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (birth is { } born)
        {
            if (born > today)
                throw new DomainException("A data de nascimento não pode estar no futuro.");
            if (born < EarliestBirth)
                throw new DomainException("A data de nascimento está fora do intervalo aceito pelo cadastro.");
        }

        if (death is { } died)
        {
            if (died > today)
                throw new DomainException("A data de falecimento não pode estar no futuro.");
            if (birth is { } bornOn && died < bornOn)
                throw new DomainException("A data de falecimento não pode ser anterior à data de nascimento.");
        }
    }

    public static void EnsurePeriod(DateOnly? start, DateOnly? end, string subject = "período")
    {
        if (start is null && end is not null)
            throw new DomainException($"A data final do {subject} exige uma data inicial.");

        if (start is { } s && end is { } e && e < s)
            throw new DomainException($"A data final do {subject} não pode ser anterior à data inicial.");

        if (start is { } started && started < EarliestMandate)
            throw new DomainException($"A data inicial do {subject} é anterior à Proclamação da República (15/11/1889).");
    }
}

public static class PublicationRules
{
    public static bool RequiresSource(ContentEntityType type) => type is
        ContentEntityType.President or
        ContentEntityType.Presidency or
        ContentEntityType.HistoricalEvent or
        ContentEntityType.Law or
        ContentEntityType.Policy or
        ContentEntityType.PresidentIndicator or
        ContentEntityType.SourcedStatement;

    public static void EnsureHasSource(int publishedSourceCount)
    {
        if (publishedSourceCount < 1)
            throw new DomainException("Não é possível publicar esta informação como registro histórico sem ao menos uma fonte publicada.");
    }
}

public static class AuthorshipRules
{
    public const string NotAuthorMessage =
        "A presença de um presidente no exercício do cargo não atribui autoria da norma.";
}

public readonly record struct IngestionFingerprint(string Adapter, string ContentHash, string? Url, string? ExternalId);

public static class IngestionDedup
{
    public static bool IsDuplicate(IEnumerable<IngestionFingerprint> existing, IngestionFingerprint candidate)
    {
        foreach (var item in existing)
        {
            if (!string.Equals(item.Adapter, candidate.Adapter, StringComparison.Ordinal))
                continue;

            if (string.Equals(item.ContentHash, candidate.ContentHash, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(candidate.Url) &&
                string.Equals(item.Url, candidate.Url, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(candidate.ExternalId) &&
                string.Equals(item.ExternalId, candidate.ExternalId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

public static class ContentHash
{
    public static string Sha256(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
