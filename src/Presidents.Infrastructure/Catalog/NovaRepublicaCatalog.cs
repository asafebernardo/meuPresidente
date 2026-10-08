using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Presidents.Domain.Catalog;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Infrastructure.Persistence;

namespace Presidents.Infrastructure.Catalog;

public static class NovaRepublicaCatalog
{
    public const string Adapter = "SenadoListaLegislacao";
    private const string ViceUrl = "https://www.gov.br/planalto/pt-br/vice-presidencia/acesso-a-informacao/institucional/biografia-dos-vice-presidentes-da-republica/biografias-vice-presidentes-versao-final.pdf";
    private const string SenateUrl = "https://legis.senado.leg.br/dadosabertos/legislacao/lista";
    private static readonly DateOnly AccessedOn = new(2026, 10, 8);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task ImportAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.IngestionRecords.AnyAsync(record => record.AdapterName == Adapter, cancellationToken))
        {
            await ReclassifyAsync(db, logger, cancellationToken);
            return;
        }

        var vice = await EnsureSourceAsync(db, "Vice-Presidência da República — biografias dos vice-presidentes (2024)",
            ViceUrl, "Vice-Presidência da República", SourceType.OfficialDocument,
            "Publicação institucional consultada em 08/10/2026 para períodos de mandato da Nova República.", cancellationToken);
        var senate = await EnsureSourceAsync(db, "Dados Abertos do Senado Federal — lista de legislação",
            SenateUrl, "Senado Federal", SourceType.Legislation,
            "Lista anual de leis, leis complementares e emendas constitucionais consultada em 08/10/2026. A ementa e a data de assinatura vêm desse serviço.", cancellationToken);
        var ipcaSource = await EnsureSourceAsync(db, "IBGE SIDRA — IPCA, tabela 1737, variável 63",
            "https://sidra.ibge.gov.br/tabela/1737", "IBGE", SourceType.OfficialDocument,
            "IPCA, variação mensal, em %. Série histórica consultada em 08/10/2026.", cancellationToken);
        var pibSource = await EnsureSourceAsync(db, "IBGE SIDRA — PIB, tabela 6784, variável 9810",
            "https://sidra.ibge.gov.br/tabela/6784", "IBGE", SourceType.OfficialDocument,
            "PIB, variação anual do volume, em %. Contas Nacionais Anuais, anos civis de 1996 a 2023.", cancellationToken);
        var jobSource = await EnsureSourceAsync(db, "IBGE SIDRA — taxa de desocupação, tabela 4099, variável 4099",
            "https://sidra.ibge.gov.br/tabela/4099", "IBGE", SourceType.OfficialDocument,
            "Taxa de desocupação trimestral das pessoas de 14 anos ou mais, PNAD Contínua, Brasil.", cancellationToken);
        var wageSource = await EnsureSourceAsync(db, "Banco Central — SGS 1619, salário mínimo",
            "https://www4.bcb.gov.br/pec/series/port/metadados/mg659ap.htm", "Banco Central do Brasil", SourceType.OfficialDocument,
            "Série 1619, salário mínimo, unidade moeda corrente, periodicidade mensal. Metadado oficial do SGS. Valores lidos na API de dados do Banco Central em 08/10/2026.", cancellationToken);

        await EnsurePeopleAsync(db, vice, cancellationToken);
        await EnsureCategoryAsync(db, "Tema não identificado", "tema-nao-identificado",
            "A ementa não contém as palavras usadas na classificação automática. Isso não é uma categoria oficial do Senado.", 26, cancellationToken);
        var indicators = await EnsureIndicatorsAsync(db, ipcaSource, pibSource, jobSource, wageSource, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await EnsureObservationsAsync(db, indicators, ipcaSource, pibSource, jobSource, wageSource, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var mandates = await db.Presidencies.AsNoTracking()
            .Where(mandate => mandate.Status == PublicationStatus.Published)
            .ToListAsync(cancellationToken);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(category => category.Slug, cancellationToken);
        var unidentified = categories["tema-nao-identificado"];
        var existing = await db.Laws.AsNoTracking()
            .Select(law => new { law.Kind, law.NumberNormalized, law.Year, law.Slug })
            .ToListAsync(cancellationToken);
        var identities = existing.Select(law => (law.Kind, law.NumberNormalized, law.Year)).ToHashSet();
        var slugs = existing.Select(law => law.Slug).ToHashSet(StringComparer.Ordinal);

        var documents = await ReadAsync<List<SenateDocument>>("senado-normas.json", cancellationToken);
        var laws = new List<Law>(400);
        var links = new List<FactSource>(400);
        var topics = new List<ContentCategoryLink>(400);
        var inserted = 0;

        foreach (var document in documents)
        {
            if (!TryMap(document, out var mapped))
                continue;
            if (identities.Contains((mapped.Kind, mapped.Digits, mapped.Date.Year)))
                continue;

            var slug = UniqueSlug($"{SlugPrefix(mapped.Kind)}-{mapped.Digits}-{mapped.Date.Year}", document.Id, slugs);
            var law = Law.Create(mapped.Kind, mapped.Number, mapped.Date.Year, mapped.Title);
            law.Slug = slug;
            law.Summary = mapped.Summary;
            law.FullTextUrl = mapped.Url;
            law.SanctionDate = mapped.Date;
            law.Origin = NormOrigin.Unknown;
            law.OperationalStatus = LawOperationalStatus.Unknown;
            law.LegislativeProcessNote = mapped.Note;
            var match = MandateCoverage.Find(mandates, mapped.Date);
            law.PresidentInOfficeId = match?.PresidentId;
            law.RebuildSearchText();
            if (law.SearchText.Length > FieldLimits.LongText)
                law.SearchText = law.SearchText[..FieldLimits.LongText];
            Publish(law, DataProvenance.Imported);

            var topicSlug = EmentaTopics.Match(mapped.Summary) ?? "tema-nao-identificado";
            if (!categories.TryGetValue(topicSlug, out var category))
                category = unidentified;

            laws.Add(law);
            links.Add(FactSource.Link(senate.Id, ContentEntityType.Law, law.Id, Cut(mapped.Summary, FieldLimits.Excerpt), "Ementa e data de assinatura da lista de legislação do Senado."));
            topics.Add(new ContentCategoryLink { EntityType = ContentEntityType.Law, EntityId = law.Id, CategoryId = category.Id, IsPrimary = true });
            identities.Add((mapped.Kind, mapped.Digits, mapped.Date.Year));
            inserted++;

            if (laws.Count >= 400)
            {
                await FlushAsync(db, laws, links, topics, cancellationToken);
                logger.LogInformation("Catálogo da Nova República: {Count} normas gravadas.", inserted);
            }
        }

        await FlushAsync(db, laws, links, topics, cancellationToken);
        await LinkLegacyHealthLawAsync(db, mandates, cancellationToken);

        db.IngestionRecords.Add(new IngestionRecord
        {
            AdapterName = Adapter,
            SourceUrl = SenateUrl,
            ExternalId = "1985-2026",
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("SenadoListaLegislacao:1985-2026"))).ToLowerInvariant(),
            CollectedAt = DateTimeOffset.UtcNow,
            SourceId = senate.Id,
            Summary = $"Catálogo importado em 08/10/2026. Normas novas nesta carga: {inserted}."
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Catálogo da Nova República concluído com {Count} normas novas.", inserted);
        await ReclassifyAsync(db, logger, cancellationToken);
    }

    private static async Task ReclassifyAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        const string version = "v2";
        if (await db.IngestionRecords.AnyAsync(record => record.AdapterName == "EmentaTopics" && record.ExternalId == version, cancellationToken))
            return;

        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(category => category.Slug, cancellationToken);
        if (!categories.TryGetValue("tema-nao-identificado", out var unidentified))
            return;

        var laws = await db.Laws.AsNoTracking()
            .Where(law => law.Provenance == DataProvenance.Imported)
            .Select(law => new { law.Id, law.Summary })
            .ToListAsync(cancellationToken);
        var links = await db.CategoryLinks
            .Where(link => link.EntityType == ContentEntityType.Law && link.IsPrimary)
            .ToListAsync(cancellationToken);
        var byLaw = links.ToDictionary(link => link.EntityId);
        var changed = 0;
        foreach (var law in laws)
        {
            if (!byLaw.TryGetValue(law.Id, out var link))
                continue;
            var slug = EmentaTopics.Match(law.Summary) ?? "tema-nao-identificado";
            if (!categories.TryGetValue(slug, out var category))
                category = unidentified;
            if (link.CategoryId == category.Id)
                continue;
            link.CategoryId = category.Id;
            changed++;
        }

        db.IngestionRecords.Add(new IngestionRecord
        {
            AdapterName = "EmentaTopics",
            ExternalId = version,
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("EmentaTopics:" + version))).ToLowerInvariant(),
            CollectedAt = DateTimeOffset.UtcNow,
            Summary = $"Reclassificação automática das ementas. Vínculos alterados: {changed}."
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Temas das ementas revistos. Vínculos alterados: {Count}.", changed);
    }

    private static async Task FlushAsync(AppDbContext db, List<Law> laws, List<FactSource> links, List<ContentCategoryLink> topics, CancellationToken cancellationToken)
    {
        if (laws.Count == 0)
            return;
        db.Laws.AddRange(laws);
        db.FactSources.AddRange(links);
        db.CategoryLinks.AddRange(topics);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        laws.Clear();
        links.Clear();
        topics.Clear();
    }

    private static async Task LinkLegacyHealthLawAsync(AppDbContext db, IReadOnlyList<Presidency> mandates, CancellationToken cancellationToken)
    {
        var law = await db.Laws.FirstOrDefaultAsync(item => item.Slug == "lei-8080-1990", cancellationToken);
        if (law is not { SanctionDate: { } date } || law.PresidentInOfficeId is not null)
            return;

        var match = MandateCoverage.Find(mandates, date);
        if (match is null)
            return;

        law.PresidentInOfficeId = match.PresidentId;
        var addition = " O presidente em exercício foi preenchido pela data de sanção e pelos períodos publicados da Nova República. A autoria continua sem atribuição.";
        law.LegislativeProcessNote = Cut((law.LegislativeProcessNote ?? string.Empty) + addition, FieldLimits.Summary);
    }

    private static async Task EnsurePeopleAsync(AppDbContext db, SourceRecord vice, CancellationToken cancellationToken)
    {
        foreach (var person in People())
        {
            if (await db.Presidents.AnyAsync(president => president.Slug == person.Slug, cancellationToken))
                continue;

            var president = President.Create(person.Name, person.FullName, person.Slug);
            president.BirthDate = person.Birth;
            president.DeathDate = person.Death;
            president.Biography = "Período presidencial registrado a partir da publicação de 2024 da Vice-Presidência da República. Esta ficha não reproduz trajetória pessoal.";
            president.HistoricalContext = person.Context;
            president.OfficialBiographyUrl = ViceUrl;
            var mandates = new List<Presidency>();
            foreach (var term in person.Terms)
            {
                var mandate = new Presidency
                {
                    President = president,
                    Ordinal = term.Ordinal,
                    VicePresident = term.Vice,
                    Party = term.Party,
                    GovernmentType = GovernmentType.NotInformed,
                    ArrivalMethod = term.Arrival,
                    PoliticalContext = term.Context,
                    DivergenceNote = term.Divergence
                };
                mandate.SetPeriod(term.Start, term.End);
                mandate.RebuildSearchText(president.FullName);
                Publish(mandate, DataProvenance.Imported);
                mandates.Add(mandate);
            }

            president.RefreshSummary(mandates);
            president.RebuildSearchText();
            Publish(president, DataProvenance.Imported);
            db.Presidents.Add(president);
            db.Presidencies.AddRange(mandates);
            db.FactSources.Add(FactSource.Link(vice.Id, ContentEntityType.President, president.Id, null, "Períodos e forma de chegada."));
            foreach (var mandate in mandates)
                db.FactSources.Add(FactSource.Link(vice.Id, ContentEntityType.Presidency, mandate.Id, null, null));
        }
    }

    private static async Task<Dictionary<string, Indicator>> EnsureIndicatorsAsync(AppDbContext db, SourceRecord ipca, SourceRecord pib, SourceRecord jobs, SourceRecord wage, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, (string Description, string Unit, SourceRecord Source)>
        {
            ["inflacao"] = ("IPCA, variação mensal, tabela 1737 do SIDRA/IBGE, variável 63. O acumulado do mandato soma os meses cujo primeiro dia cai no período.", "%", ipca),
            ["pib"] = ("PIB, variação anual do volume, tabela 6784 do SIDRA/IBGE, variável 9810. Entram só os anos civis inteiros cobertos pelo mandato. A série publicada vai de 1996 a 2023.", "%", pib),
            ["desemprego"] = ("Taxa de desocupação trimestral, PNAD Contínua, tabela 4099 do SIDRA/IBGE, variável 4099. A série começa em 2012.", "%", jobs),
            ["salario-minimo"] = ("Salário mínimo nominal, série SGS 1619 do Banco Central. Antes de 01/07/1994 a série atravessa outros padrões monetários.", "moeda corrente", wage)
        };

        var names = new Dictionary<string, string>
        {
            ["inflacao"] = "Inflação",
            ["pib"] = "PIB",
            ["desemprego"] = "Desemprego",
            ["salario-minimo"] = "Salário mínimo"
        };
        var result = new Dictionary<string, Indicator>();
        foreach (var (slug, spec) in map)
        {
            var indicator = await db.Indicators.FirstOrDefaultAsync(item => item.Slug == slug, cancellationToken);
            if (indicator is null)
            {
                indicator = Indicator.Create(names[slug], spec.Unit, spec.Description);
                Publish(indicator, DataProvenance.SystemTaxonomy);
                db.Indicators.Add(indicator);
            }

            indicator.Description = spec.Description;
            indicator.Unit = spec.Unit;
            indicator.MethodologySourceId = spec.Source.Id;
            indicator.RebuildSearchText();
            result[slug] = indicator;
        }

        return result;
    }

    private static async Task EnsureObservationsAsync(AppDbContext db, IReadOnlyDictionary<string, Indicator> indicators, SourceRecord ipca, SourceRecord pib, SourceRecord jobs, SourceRecord wage, CancellationToken cancellationToken)
    {
        var file = await ReadAsync<IndicatorFile>("indicadores.json", cancellationToken);
        await AddDatedAsync(db, indicators["inflacao"], ipca.Id, file.IpcaMensal, "Mês de referência do IPCA.", cancellationToken);
        await AddDatedAsync(db, indicators["salario-minimo"], wage.Id, file.SalarioMinimo, "Valor nominal do mês na série SGS 1619.", cancellationToken);
        await AddAnnualAsync(db, indicators["pib"], pib.Id, file.PibVolumeAnual, cancellationToken);
        await AddQuartersAsync(db, indicators["desemprego"], jobs.Id, file.DesocupacaoTrimestral, cancellationToken);
    }

    private static async Task AddDatedAsync(AppDbContext db, Indicator indicator, Guid sourceId, IEnumerable<DatedPoint> points, string note, CancellationToken cancellationToken)
    {
        var existing = await db.IndicatorObservations.Where(item => item.IndicatorId == indicator.Id).Select(item => item.ReferenceDate).ToListAsync(cancellationToken);
        var known = existing.ToHashSet();
        foreach (var point in points)
        {
            if (!DateOnly.TryParseExact(point.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!decimal.TryParse(point.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || !known.Add(date))
                continue;
            db.IndicatorObservations.Add(Observation(indicator.Id, sourceId, date, value, note));
        }
    }

    private static async Task AddAnnualAsync(AppDbContext db, Indicator indicator, Guid sourceId, IEnumerable<AnnualPoint> points, CancellationToken cancellationToken)
    {
        var existing = await db.IndicatorObservations.Where(item => item.IndicatorId == indicator.Id).Select(item => item.ReferenceDate).ToListAsync(cancellationToken);
        var known = existing.ToHashSet();
        foreach (var point in points)
        {
            var date = new DateOnly(point.Year, 1, 1);
            if (!decimal.TryParse(point.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || !known.Add(date))
                continue;
            db.IndicatorObservations.Add(Observation(indicator.Id, sourceId, date, value, "Variação anual do volume do PIB."));
        }
    }

    private static async Task AddQuartersAsync(AppDbContext db, Indicator indicator, Guid sourceId, IEnumerable<QuarterPoint> points, CancellationToken cancellationToken)
    {
        var existing = await db.IndicatorObservations.Where(item => item.IndicatorId == indicator.Id).Select(item => item.ReferenceDate).ToListAsync(cancellationToken);
        var known = existing.ToHashSet();
        foreach (var point in points)
        {
            if (point.Period.Length != 6 || !int.TryParse(point.Period[..4], out var year) || !int.TryParse(point.Period[4..], out var quarter) || quarter is < 1 or > 4)
                continue;
            var date = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
            if (!decimal.TryParse(point.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || !known.Add(date))
                continue;
            db.IndicatorObservations.Add(Observation(indicator.Id, sourceId, date, value, $"Trimestre {quarter} de {year}."));
        }
    }

    private static IndicatorObservation Observation(Guid indicatorId, Guid sourceId, DateOnly date, decimal value, string note)
    {
        var observation = new IndicatorObservation
        {
            IndicatorId = indicatorId,
            SourceId = sourceId,
            ReferenceDate = date,
            Value = value,
            Note = note
        };
        Publish(observation, DataProvenance.Imported);
        return observation;
    }

    private static async Task EnsureCategoryAsync(AppDbContext db, string name, string slug, string description, int sort, CancellationToken cancellationToken)
    {
        if (await db.Categories.AnyAsync(category => category.Slug == slug, cancellationToken))
            return;
        var category = Category.Create(name, description, slug);
        category.SortOrder = sort;
        Publish(category, DataProvenance.SystemTaxonomy);
        db.Categories.Add(category);
    }

    private static async Task<SourceRecord> EnsureSourceAsync(AppDbContext db, string name, string url, string publisher, SourceType type, string notes, CancellationToken cancellationToken)
    {
        var existing = await db.Sources.FirstOrDefaultAsync(source => source.Url == url, cancellationToken);
        if (existing is not null)
            return existing;

        var source = SourceRecord.Create(name, type, ReliabilityLevel.Primary, url);
        source.Publisher = publisher;
        source.Title = name;
        source.AccessedAt = AccessedOn;
        source.Notes = notes;
        source.RebuildSearchText();
        Publish(source, DataProvenance.Imported);
        db.Sources.Add(source);
        return source;
    }

    private static bool TryMap(SenateDocument document, out MappedLaw mapped)
    {
        mapped = default;
        var kind = document.Tipo switch
        {
            "LEI-n" => NormKind.OrdinaryLaw,
            "LCP" => NormKind.ComplementaryLaw,
            "EMC-n" => NormKind.ConstitutionalAmendment,
            _ => (NormKind?)null
        };
        if (kind is null || string.IsNullOrWhiteSpace(document.Numero))
            return false;
        if (!DateOnly.TryParseExact(document.Dataassinatura, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;
        if (date < MandateCoverage.Redemocratization)
            return false;

        var digits = TextNormalizer.Digits(document.Numero);
        if (digits.Length == 0 || !long.TryParse(digits, out var numeric))
            return false;

        var number = numeric.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
        var title = Cut(string.IsNullOrWhiteSpace(document.NormaNome) ? $"{kind} {number}/{date.Year}" : document.NormaNome, FieldLimits.Title);
        var summary = Cut(document.Ementa ?? title, FieldLimits.Summary);
        var note = $"Lista de legislação do Senado, id {document.Id}. Tema classificado automaticamente por palavras da ementa; não é o índice oficial do Senado. O presidente em exercício, quando preenchido, é o do período que contém a data de assinatura. Isso não atribui autoria.";
        if (date < new DateOnly(1985, 4, 21))
            note += " A publicação da Vice-Presidência registra a assunção de José Sarney em 21/04/1985, então esta data fica sem presidente em exercício no cadastro.";

        mapped = new MappedLaw(kind.Value, number, digits, date, title, summary, FullText(kind.Value, digits, date), Cut(note, FieldLimits.Summary));
        return true;
    }

    private static string? FullText(NormKind kind, string digits, DateOnly date)
    {
        var lex = kind switch
        {
            NormKind.OrdinaryLaw => "lei",
            NormKind.ComplementaryLaw => "lei.complementar",
            NormKind.ConstitutionalAmendment => "emenda.constitucional",
            _ => null
        };
        return lex is null ? null : $"https://normas.leg.br/?urn=urn:lex:br:federal:{lex}:{date:yyyy-MM-dd};{digits}";
    }

    private static string SlugPrefix(NormKind kind) => kind switch
    {
        NormKind.OrdinaryLaw => "lei",
        NormKind.ComplementaryLaw => "lei-complementar",
        NormKind.ConstitutionalAmendment => "emenda-constitucional",
        _ => "norma"
    };

    private static string UniqueSlug(string slug, string? id, HashSet<string> slugs)
    {
        var candidate = slug.Length > FieldLimits.Slug ? slug[..FieldLimits.Slug].Trim('-') : slug;
        if (slugs.Add(candidate))
            return candidate;
        var suffix = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N")[..8] : id;
        candidate = Cut($"{slug}-{suffix}", FieldLimits.Slug).Trim('-');
        slugs.Add(candidate);
        return candidate;
    }

    private static string Cut(string value, int max)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= max)
            return trimmed;
        return trimmed[..(max - 1)].TrimEnd() + "…";
    }

    private static void Publish(PublishableEntity entity, DataProvenance provenance)
    {
        entity.Status = PublicationStatus.Published;
        entity.PublishedAt = DateTimeOffset.UtcNow;
        entity.Provenance = provenance;
    }

    private static async Task<T> ReadAsync<T>(string suffix, CancellationToken cancellationToken)
    {
        var assembly = typeof(NovaRepublicaCatalog).Assembly;
        var name = assembly.GetManifestResourceNames().Single(item => item.EndsWith(suffix, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Recurso {suffix} ausente.");
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? throw new InvalidOperationException($"Recurso {suffix} vazio.");
    }

    private static IEnumerable<PersonSpec> People()
    {
        yield return new PersonSpec("José Sarney", "José Ribamar Ferreira de Araújo Costa", "jose-sarney", new DateOnly(1930, 4, 24), null,
            "A publicação registra o ingresso no PMDB e a assunção da Presidência em 21/04/1985, depois da morte de Tancredo Neves antes da posse.",
            [
                new TermSpec(1, new DateOnly(1985, 4, 21), new DateOnly(1990, 3, 15), null, "PMDB", ArrivalMethod.VicePresidentialSuccession,
                    "A publicação diz que Sarney assumiu em 21/04/1985. O fim cadastrado é o início do mandato seguinte no mesmo documento, 15/03/1990. A ficha vice-presidencial cobre 15/03/1985 a 21/04/1985, sob Tancredo de Almeida Neves.",
                    "O documento distingue o intervalo como vice, até 21/04/1985, da assunção da Presidência nessa data.")
            ]);
        yield return new PersonSpec("Fernando Collor", "Fernando Collor de Mello", "fernando-collor", null, null,
            "A publicação registra a chapa com Itamar Franco na legenda do PRN e o afastamento que levou à assunção provisória de Itamar em 02/10/1992.",
            [
                new TermSpec(1, new DateOnly(1990, 3, 15), new DateOnly(1992, 10, 2), "Itamar Franco", "PRN", ArrivalMethod.DirectElection,
                    "Vice-presidência de Itamar Franco registrada de 15/03/1990 a 29/12/1992. O texto diz que ele assumiu a Presidência em caráter provisório em 02/10/1992. Este período usa essa data como fim.",
                    "A ficha técnica grafa Fernando Afonso Collor de Melo. O texto corrido grafa Fernando Collor de Mello, nome usado neste cadastro.")
            ]);
        yield return new PersonSpec("Itamar Franco", "Itamar Augusto Cautiero Franco", "itamar-franco", new DateOnly(1930, 6, 28), new DateOnly(2011, 7, 2),
            "A publicação registra assunção provisória em 02/10/1992 e efetivação em 29/12/1992, após a renúncia de Collor.",
            [
                new TermSpec(1, new DateOnly(1992, 10, 2), new DateOnly(1995, 1, 1), null, null, ArrivalMethod.VicePresidentialSuccession,
                    "O texto registra a assunção provisória em 02/10/1992 e a efetivação em 29/12/1992. O fim cadastrado, 01/01/1995, é o início do mandato seguinte no mesmo documento.",
                    "A ficha vice-presidencial de Itamar termina em 29/12/1992. O intervalo presidencial cadastrado continua até 01/01/1995.")
            ]);
        yield return new PersonSpec("Luiz Inácio Lula da Silva", "Luiz Inácio Lula da Silva", "luiz-inacio-lula-da-silva", null, null,
            "A publicação identifica Lula pelo PT na chapa de 2003 e registra três intervalos: a partir de 01/01/2003, o segundo mandato em 01/01/2007 e o período 2023-2026.",
            [
                new TermSpec(1, new DateOnly(2003, 1, 1), new DateOnly(2007, 1, 1), "José Alencar", "PT", ArrivalMethod.DirectElection,
                    "O documento diz que Lula e José Alencar tomaram posse em 01/01/2003. O segundo mandato vice-presidencial começa em 01/01/2007.", null),
                new TermSpec(2, new DateOnly(2007, 1, 1), new DateOnly(2011, 1, 1), "José Alencar", "PT", ArrivalMethod.DirectElection,
                    "O documento registra a assunção do segundo mandato vice-presidencial em 01/01/2007. O fim cadastrado é o início do mandato seguinte, 01/01/2011.", null),
                new TermSpec(3, new DateOnly(2023, 1, 1), new DateOnly(2026, 12, 31), "Geraldo José Rodrigues Alckmin Filho", "PT", ArrivalMethod.DirectElection,
                    "O documento registra Geraldo Alckmin como vice desde 01/01/2023, para o período 2023-2026. A ficha termina em 31/12/2026.", null)
            ]);
        yield return new PersonSpec("Dilma Rousseff", "Dilma Vana Rousseff", "dilma-rousseff", null, null,
            "A publicação registra Michel Temer eleito vice em 2010 e reeleito em 2014 com Dilma Vana Rousseff, e a assunção dele em 31/08/2016.",
            [
                new TermSpec(1, new DateOnly(2011, 1, 1), new DateOnly(2016, 8, 31), "Michel Temer", null, ArrivalMethod.DirectElection,
                    "O documento cita eleição em 2010 e reeleição em 2014, sem uma data separada de posse do segundo mandato. O intervalo cadastrado vai de 01/01/2011 até a assunção de Temer em 31/08/2016.",
                    "A reeleição de 2014 está no texto, mas a publicação não informa o dia de início do segundo mandato. Por isso os dois períodos não foram separados.")
            ]);
        yield return new PersonSpec("Michel Temer", "Michel Miguel Elias Temer Lulia", "michel-temer", new DateOnly(1940, 9, 23), null,
            "A publicação diz que Michel Temer assumiu definitivamente a Presidência em 31/08/2016, após o impeachment de Dilma Rousseff.",
            [
                new TermSpec(1, new DateOnly(2016, 8, 31), new DateOnly(2019, 1, 1), null, null, ArrivalMethod.VicePresidentialSuccession,
                    "Assunção em 31/08/2016. O fim cadastrado, 01/01/2019, é o início do mandato seguinte no mesmo documento.", null)
            ]);
        yield return new PersonSpec("Jair Bolsonaro", "Jair Messias Bolsonaro", "jair-bolsonaro", null, null,
            "A publicação registra a chapa com Hamilton Mourão e identifica Jair Messias Bolsonaro, do PSL, na eleição de 2018.",
            [
                new TermSpec(1, new DateOnly(2019, 1, 1), new DateOnly(2023, 1, 1), "Antônio Hamilton Martins Mourão", "PSL", ArrivalMethod.DirectElection,
                    "Posse vice-presidencial em 01/01/2019 e ficha até 31/12/2022. O texto diz que a filiação citada ao PSL valia 'até então'.",
                    "O partido registrado é o da chapa na publicação, com a ressalva 'até então'. Mudança posterior de partido não está nesse documento.")
            ]);
    }

    private readonly record struct MappedLaw(NormKind Kind, string Number, string Digits, DateOnly Date, string Title, string Summary, string? Url, string Note);
    private sealed record TermSpec(int Ordinal, DateOnly Start, DateOnly End, string? Vice, string? Party, ArrivalMethod Arrival, string Context, string? Divergence);
    private sealed record PersonSpec(string Name, string FullName, string Slug, DateOnly? Birth, DateOnly? Death, string Context, TermSpec[] Terms);
    private sealed class SenateDocument
    {
        public string? Id { get; set; }
        public string? Tipo { get; set; }
        public string? Numero { get; set; }
        public string? NormaNome { get; set; }
        public string? Ementa { get; set; }
        public string? Dataassinatura { get; set; }
    }

    private sealed class IndicatorFile
    {
        public List<DatedPoint> IpcaMensal { get; set; } = [];
        public List<AnnualPoint> PibVolumeAnual { get; set; } = [];
        public List<QuarterPoint> DesocupacaoTrimestral { get; set; } = [];
        public List<DatedPoint> SalarioMinimo { get; set; } = [];
    }

    private sealed class DatedPoint
    {
        public string Date { get; set; } = "";
        public string Value { get; set; } = "";
    }

    private sealed class AnnualPoint
    {
        public int Year { get; set; }
        public string Value { get; set; } = "";
    }

    private sealed class QuarterPoint
    {
        public string Period { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
