using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Presidents.Application.Contracts;
using Presidents.Application.Rag;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Rules;
using Presidents.Infrastructure.Persistence;

namespace Presidents.Infrastructure.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";
    public string[] AllowedHosts { get; set; } = OfficialHosts.Defaults;
}

public static class OfficialHosts
{
    public static readonly string[] Defaults =
    [
        "www.planalto.gov.br",
        "planalto.gov.br",
        "www4.planalto.gov.br",
        "www.gov.br",
        "www.in.gov.br",
        "in.gov.br",
        "www.senado.leg.br",
        "legis.senado.leg.br",
        "www25.senado.leg.br",
        "www.camara.leg.br",
        "www2.camara.leg.br",
        "www.biblioteca.presidencia.gov.br",
        "www.bcb.gov.br",
        "www.ibge.gov.br",
        "www.ipea.gov.br",
        "www.tse.jus.br"
    ];
}

internal static class ImportGuards
{
    public static IReadOnlyCollection<string> Hosts(IngestionOptions options) =>
        options.AllowedHosts is { Length: > 0 } ? options.AllowedHosts : OfficialHosts.Defaults;

    public static void EnsureOfficialUrl(string? url, IngestionOptions options)
    {
        if (!IsAllowed(url, Hosts(options)))
            throw new DomainException("A importação de legislação oficial só aceita URLs dos domínios configurados em Ingestion:AllowedHosts.");
    }

    public static bool IsAllowed(string? url, IReadOnlyCollection<string> hosts)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return hosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase) || hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }

    public static string Hash(ImportRequest request) =>
        ContentHash.Sha256(string.Join('\n', request.Url, request.ExternalId, request.Title, request.Summary, request.Number, request.Year, request.Excerpt));
}

public sealed class GovernmentLegislationImporter(AppDbContext db, IOptions<IngestionOptions> options) : IDataImporter
{
    public string AdapterName => "GovernmentLegislationImporter";
    public ImportChannel Channel => ImportChannel.Legislation;

    public async Task<ImportResultDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        ImportGuards.EnsureOfficialUrl(request.Url, options.Value);
        if (request.RawContent is { Length: > 20000 })
            throw new DomainException("O recorte de legislação excede 20 mil caracteres. Guarde a URL oficial e um resumo próprio.");

        return await ImportCoreAsync(request, SourceType.Legislation, ReliabilityLevel.Primary, storeRaw: true, cancellationToken);
    }

    internal async Task<ImportResultDto> ImportCoreAsync(ImportRequest request, SourceType sourceType, ReliabilityLevel reliability, bool storeRaw, CancellationToken cancellationToken)
    {
        var hash = ImportGuards.Hash(request);
        var fingerprints = await db.IngestionRecords.AsNoTracking()
            .Where(record => record.AdapterName == AdapterName)
            .Select(record => new IngestionFingerprint(record.AdapterName, record.ContentHash, record.SourceUrl, record.ExternalId))
            .ToListAsync(cancellationToken);
        if (IngestionDedup.IsDuplicate(fingerprints, new IngestionFingerprint(AdapterName, hash, request.Url, request.ExternalId)))
            return new ImportResultDto(false, true, null, "Registro já importado. A URL, o identificador externo ou o hash coincidem com uma coleta anterior.");

        var source = SourceRecord.Create(request.Title ?? request.Publisher ?? "Fonte importada", sourceType, reliability, request.Url);
        source.Title = request.Title;
        source.Publisher = request.Publisher;
        source.Author = request.Author;
        source.PublicationDate = request.PublicationDate;
        source.AccessedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        source.Provenance = DataProvenance.Imported;
        source.Notes = "Importado como rascunho. Não publicado automaticamente.";
        source.RebuildSearchText();
        db.Sources.Add(source);

        Law? law = null;
        if (!string.IsNullOrWhiteSpace(request.Number) && request.Year is { } year && !string.IsNullOrWhiteSpace(request.Title))
        {
            law = Law.Create(request.Kind, request.Number, year, request.Title);
            law.Summary = request.Summary;
            law.FullTextUrl = request.Url;
            law.Origin = NormOrigin.Unknown;
            law.PresidentInOfficeId = request.PresidentInOfficeId;
            law.AttributedAsAuthor = false;
            law.Provenance = DataProvenance.Imported;
            law.LegislativeProcessNote = "Registro importado como rascunho. A autoria não foi atribuída automaticamente ao presidente em exercício.";
            law.RebuildSearchText();
            db.Laws.Add(law);
            db.FactSources.Add(FactSource.Link(source.Id, ContentEntityType.Law, law.Id, request.Excerpt, "Fonte informada na importação."));
        }

        var record = new IngestionRecord
        {
            AdapterName = AdapterName,
            SourceUrl = request.Url,
            ExternalId = request.ExternalId,
            ContentHash = hash,
            CollectedAt = DateTimeOffset.UtcNow,
            Summary = request.Summary,
            RawContent = storeRaw ? request.RawContent : null,
            Source = source,
            Law = law
        };
        db.IngestionRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(true, false, record.Id, "Importação gravada como rascunho. Revise e publique somente depois de conferir a fonte.");
    }
}

public sealed class NewsImporter(AppDbContext db) : IDataImporter
{
    public string AdapterName => "NewsImporter";
    public ImportChannel Channel => ImportChannel.News;

    public Task<ImportResultDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RawContent))
            throw new DomainException("Artigos jornalísticos não são armazenados integralmente. Informe título, URL, autoria, data e um resumo próprio.");
        if (string.IsNullOrWhiteSpace(request.Summary))
            throw new DomainException("A importação de notícia exige um resumo próprio, não a reprodução do texto.");
        if (string.IsNullOrWhiteSpace(request.Excerpt))
            throw new DomainException("Informe apenas um trecho curto, de até 400 caracteres, quando ele for legalmente permitido.");
        if (request.Excerpt.Length > FieldLimits.Excerpt)
            throw new DomainException("O trecho de notícia não pode ultrapassar 400 caracteres.");
        if (string.IsNullOrWhiteSpace(request.Url))
            throw new DomainException("A notícia precisa apontar para a URL original.");

        return new JournalImporter(db).ImportAsync(request, cancellationToken);
    }

    private sealed class JournalImporter(AppDbContext db) : IDataImporter
    {
        public string AdapterName => "NewsImporter";
        public ImportChannel Channel => ImportChannel.News;

        public async Task<ImportResultDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken)
        {
            var hash = ImportGuards.Hash(request);
            var fingerprints = await db.IngestionRecords.AsNoTracking()
                .Where(record => record.AdapterName == AdapterName)
                .Select(record => new IngestionFingerprint(record.AdapterName, record.ContentHash, record.SourceUrl, record.ExternalId))
                .ToListAsync(cancellationToken);
            if (IngestionDedup.IsDuplicate(fingerprints, new IngestionFingerprint(AdapterName, hash, request.Url, request.ExternalId)))
                return new ImportResultDto(false, true, null, "Esta notícia já foi importada.");

            var source = SourceRecord.Create(request.Title ?? "Notícia", SourceType.Newspaper, ReliabilityLevel.Secondary, request.Url);
            source.Title = request.Title;
            source.Publisher = request.Publisher;
            source.Author = request.Author;
            source.PublicationDate = request.PublicationDate;
            source.AccessedAt = DateOnly.FromDateTime(DateTime.UtcNow);
            source.Notes = request.Summary;
            source.Provenance = DataProvenance.Imported;
            source.RebuildSearchText();
            db.Sources.Add(source);
            var record = new IngestionRecord
            {
                AdapterName = AdapterName,
                SourceUrl = request.Url,
                ExternalId = request.ExternalId,
                ContentHash = hash,
                CollectedAt = DateTimeOffset.UtcNow,
                Summary = request.Summary,
                RawContent = null,
                Source = source
            };
            db.IngestionRecords.Add(record);
            await db.SaveChangesAsync(cancellationToken);
            return new ImportResultDto(true, false, record.Id, "Notícia registrada como rascunho, com resumo próprio e link para o original. O texto integral não foi armazenado.");
        }
    }
}

public sealed class AcademicSourceImporter(AppDbContext db) : IDataImporter
{
    public string AdapterName => "AcademicSourceImporter";
    public ImportChannel Channel => ImportChannel.Academic;

    public async Task<ImportResultDto> ImportAsync(ImportRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.RawContent))
            throw new DomainException("Obras acadêmicas não são copiadas integralmente. Guarde referência, URL e um resumo próprio.");
        if (string.IsNullOrWhiteSpace(request.Summary) || string.IsNullOrWhiteSpace(request.Title))
            throw new DomainException("A fonte acadêmica exige título e resumo próprio.");
        if (request.Excerpt is { Length: > FieldLimits.Excerpt })
            throw new DomainException("O trecho acadêmico não pode ultrapassar 400 caracteres.");

        var hash = ImportGuards.Hash(request);
        var fingerprints = await db.IngestionRecords.AsNoTracking()
            .Where(record => record.AdapterName == AdapterName)
            .Select(record => new IngestionFingerprint(record.AdapterName, record.ContentHash, record.SourceUrl, record.ExternalId))
            .ToListAsync(cancellationToken);
        if (IngestionDedup.IsDuplicate(fingerprints, new IngestionFingerprint(AdapterName, hash, request.Url, request.ExternalId)))
            return new ImportResultDto(false, true, null, "Esta fonte acadêmica já foi importada.");

        var source = SourceRecord.Create(request.Title, SourceType.Academic, ReliabilityLevel.Secondary, request.Url);
        source.Title = request.Title;
        source.Author = request.Author;
        source.Publisher = request.Publisher;
        source.PublicationDate = request.PublicationDate;
        source.AccessedAt = DateOnly.FromDateTime(DateTime.UtcNow);
        source.Notes = request.Summary;
        source.Provenance = DataProvenance.Imported;
        source.RebuildSearchText();
        db.Sources.Add(source);
        var record = new IngestionRecord
        {
            AdapterName = AdapterName,
            SourceUrl = request.Url,
            ExternalId = request.ExternalId,
            ContentHash = hash,
            CollectedAt = DateTimeOffset.UtcNow,
            Summary = request.Summary,
            RawContent = null,
            Source = source
        };
        db.IngestionRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(true, false, record.Id, "Fonte acadêmica registrada como rascunho, sem o texto integral.");
    }
}

public sealed class SelectingLlmClient(HttpClient http, IOptions<AiOptions> options) : ILlmClient
{
    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<IReadOnlyList<Guid>> SelectSourcesAsync(string question, EvidencePack pack, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
            return [];

        var evidence = pack.Items.Select(item => new { id = item.SourceId, text = item.Statement, name = item.SourceName }).ToList();
        var system = "Você seleciona evidências já recuperadas de um banco histórico. Responda apenas JSON {\"sourceIds\":[\"guid\"]}. Não escreva fatos, não crie fontes e não use ids que não estejam na lista. Se nada servir, devolva {\"sourceIds\":[]}.";
        var user = JsonSerializer.Serialize(new { question, evidence });
        var payload = new
        {
            model = string.IsNullOrWhiteSpace(options.Value.Model) ? "gpt-4.1-mini" : options.Value.Model,
            temperature = 0,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = JsonContent.Create(payload) };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"O modelo retornou {(int)response.StatusCode}.");

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var jsonStart = content.IndexOf('{');
        var jsonEnd = content.LastIndexOf('}');
        if (jsonStart < 0 || jsonEnd <= jsonStart)
            throw new InvalidOperationException("O modelo não devolveu JSON de seleção.");

        using var selected = JsonDocument.Parse(content[jsonStart..(jsonEnd + 1)]);
        var allowed = pack.Items.Select(item => item.SourceId).ToHashSet();
        var ids = new List<Guid>();
        if (selected.RootElement.TryGetProperty("sourceIds", out var array))
        {
            foreach (var item in array.EnumerateArray())
            {
                if (Guid.TryParse(item.GetString(), out var id) && allowed.Contains(id))
                    ids.Add(id);
            }
        }

        return ids;
    }
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4.1-mini";
    public string ApiKey { get; set; } = "";
}
