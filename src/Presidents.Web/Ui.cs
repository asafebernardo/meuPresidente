using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.WebUtilities;
using Presidents.Domain.Common;
using Presidents.Domain.Enums;

namespace Presidents.Web;

public static class Ui
{
    public static string Date(DateOnly? value) => value?.ToString("dd/MM/yyyy") ?? "não informada";

    public static string Message(Exception exception) => exception switch
    {
        ValidationException validation => string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct()),
        DomainException domain => domain.Message,
        _ => "Não foi possível concluir a operação."
    };

    public static string KindClass(string? label) => label switch
    {
        "Fato documentado" or "Dado oficial" => "fact",
        "Interpretação histórica" or "Informação jornalística" => "interpretation",
        "Crítica de grupo" => "criticism",
        "Opinião" => "opinion",
        _ => ""
    };

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
    }

    public static string Json(object value) =>
        JsonSerializer.Serialize(value).Replace("<", "\\u003c", StringComparison.Ordinal);

    public static string WithQuery(string path, IEnumerable<KeyValuePair<string, string?>> values)
    {
        var kept = values
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value));
        var query = QueryHelpers.AddQueryString(path, kept);
        return query;
    }
}

public static class AdminCommand
{
    public static bool TryParse(string? action, out string verb, out Guid id)
    {
        verb = string.Empty;
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(action))
            return false;
        var parts = action.Split(':', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
            return false;
        verb = parts[0];
        return verb is "review" or "publish" or "draft" or "archive" or "delete";
    }

    public static PublicationStatus? ToStatus(string verb) => verb switch
    {
        "review" => PublicationStatus.Review,
        "publish" => PublicationStatus.Published,
        "draft" => PublicationStatus.Draft,
        "archive" => PublicationStatus.Archived,
        _ => null
    };
}

public sealed class RowCommand
{
    public string Action { get; set; } = string.Empty;
}
