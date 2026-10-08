using Microsoft.EntityFrameworkCore;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;

namespace Presidents.Infrastructure.Persistence;

public static class DemoDataSeeder
{
    private static readonly DateOnly AccessedOn = new(2026, 10, 7);

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Presidents.AnyAsync(president => president.FullName == "Getúlio Dornelles Vargas", cancellationToken))
            return;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var categories = SeedCategories();
        db.Categories.AddRange(categories);
        db.Indicators.AddRange(SeedIndicators());

        var vargasPage = Source("Biblioteca da Presidência — Getúlio Dornelles Vargas",
            "https://www.biblioteca.presidencia.gov.br/presidencia/ex-presidentes/getulio-vargas",
            "Biblioteca da Presidência da República");
        var vargasPdf = Source("Biblioteca da Presidência — biografia em inglês de Getúlio Dornelles Vargas",
            "https://www.biblioteca.presidencia.gov.br/presidencia/biografia-ex-presidentes-em-ingles/getulio-dornelles-vargas.pdf",
            "Biblioteca da Presidência da República");
        var jkPage = Source("Biblioteca da Presidência — Juscelino Kubitschek",
            "https://www.biblioteca.presidencia.gov.br/presidencia/ex-presidentes/jk",
            "Biblioteca da Presidência da República");
        var jkArchive = Source("Arquivo Nacional — biografia de Juscelino Kubitschek de Oliveira",
            "https://presidentes.an.gov.br/index.php/centro-de-referencia-de-acervos-presidenciais/assuntos/biografias/182-juscelino-kubitschek-de-oliveira",
            "Arquivo Nacional");
        var fhcPage = Source("Biblioteca da Presidência — Fernando Henrique Cardoso",
            "https://www.biblioteca.presidencia.gov.br/presidencia/ex-presidentes/fernando-henrique-cardoso",
            "Biblioteca da Presidência da República");
        var fhcVice = Source("Biblioteca da Presidência — vice-presidência no governo Fernando Henrique Cardoso",
            "https://www.biblioteca.presidencia.gov.br/presidencia/ex-presidentes/fernando-henrique-cardoso/vice-presidente",
            "Biblioteca da Presidência da República");
        var lawPage = Source("Portal da Legislação — Lei nº 8.080, de 19 de setembro de 1990",
            "https://www.planalto.gov.br/ccivil_03/leis/l8080.htm",
            "Portal da Legislação / Presidência da República",
            SourceType.Legislation);

        var vargas = President.Create("Getúlio Vargas", "Getúlio Dornelles Vargas", "getulio-vargas");
        vargas.BirthDate = new DateOnly(1883, 4, 19);
        vargas.DeathDate = new DateOnly(1954, 8, 24);
        vargas.Biography = "Advogado, nascido em São Borja (RS). Este cadastro de demonstração reúne apenas períodos e datas localizados na Biblioteca da Presidência da República. Não resume políticas públicas.";
        vargas.HistoricalContext = "A página em português da Biblioteca da Presidência registra o governo provisório de 03/11/1930 a 20/07/1934 e o falecimento em 24/08/1954, no Rio de Janeiro. A biografia em inglês da mesma biblioteca registra o início do Estado Novo em 10/11/1937, a saída do cargo em 29/10/1945 e a posse em 31/01/1951. O intervalo entre julho de 1934 e novembro de 1937 não foi cadastrado: as páginas usadas no seed não apresentam, no trecho consultado, um único intervalo explícito para esse período. A lacuna é intencional.";
        vargas.OfficialBiographyUrl = vargasPage.Url;
        Publish(vargas, DataProvenance.DemonstrationSeed);

        var jk = President.Create("Juscelino Kubitschek", "Juscelino Kubitschek de Oliveira", "juscelino-kubitschek");
        jk.BirthDate = new DateOnly(1902, 9, 12);
        jk.DeathDate = new DateOnly(1976, 8, 22);
        jk.Biography = "Médico, nascido em Diamantina (MG). A Biblioteca da Presidência registra o período de governo de 31/01/1956 a 31/01/1961 e a vice-presidência de João Belchior Marques Goulart. A biografia do Arquivo Nacional registra a coligação PSD-PTB.";
        jk.OfficialBiographyUrl = jkPage.Url;
        Publish(jk, DataProvenance.DemonstrationSeed);

        var fhc = President.Create("Fernando Henrique Cardoso", "Fernando Henrique Cardoso", "fernando-henrique-cardoso");
        fhc.BirthDate = new DateOnly(1931, 6, 18);
        fhc.Biography = "Sociólogo, nascido no Rio de Janeiro (RJ). A Biblioteca da Presidência registra dois mandatos, de 01/01/1995 a 01/01/1999 e de 01/01/1999 a 01/01/2003, com vice-presidência de Marco Antonio de Oliveira Maciel e eleição direta.";
        fhc.OfficialBiographyUrl = fhcPage.Url;
        Publish(fhc, DataProvenance.DemonstrationSeed);

        var vargasProvisional = Mandate(vargas, 1, new DateOnly(1930, 11, 3), new DateOnly(1934, 7, 20), null, null,
            GovernmentType.Provisional, ArrivalMethod.ProvisionalGovernment,
            "A Biblioteca da Presidência denomina este intervalo como governo provisório, de 03/11/1930 a 20/07/1934.");
        var estadoNovo = Mandate(vargas, 2, new DateOnly(1937, 11, 10), new DateOnly(1945, 10, 29), null, null,
            GovernmentType.EstadoNovo, ArrivalMethod.Other,
            "A biografia em inglês da Biblioteca da Presidência registra que, em 10/11/1937, Vargas anunciou a dissolução do Congresso e o início do Estado Novo, e que deixou o cargo em 29/10/1945.");
        var vargas1951 = Mandate(vargas, 3, new DateOnly(1951, 1, 31), new DateOnly(1954, 8, 24), null, "PTB",
            GovernmentType.NotInformed, ArrivalMethod.DirectElection,
            "A biografia em inglês da Biblioteca da Presidência registra a posse em 31/01/1951 pelo PTB. A data final deste cadastro é a de falecimento registrada pela Biblioteca (24/08/1954), não uma afirmação autônoma sobre o encerramento formal do mandato.");
        var jkMandate = Mandate(jk, 1, new DateOnly(1956, 1, 31), new DateOnly(1961, 1, 31), "João Belchior Marques Goulart", "PSD",
            GovernmentType.NotInformed, ArrivalMethod.NotInformed,
            "A Biblioteca da Presidência registra o Décimo Sexto Período de Governo Republicano, de 31/01/1956 a 31/01/1961, com vice-presidência de João Belchior Marques Goulart. A biografia do Arquivo Nacional registra a coligação PSD-PTB. A forma de eleição não foi classificada além do que essas páginas dizem.");
        var fhcFirst = Mandate(fhc, 1, new DateOnly(1995, 1, 1), new DateOnly(1999, 1, 1), "Marco Antonio de Oliveira Maciel", "PSDB",
            GovernmentType.NotInformed, ArrivalMethod.DirectElection,
            "A Biblioteca da Presidência registra eleição direta, a coligação PSDB/PFL/PTB e o período de 01/01/1995 a 01/01/1999.");
        var fhcSecond = Mandate(fhc, 2, new DateOnly(1999, 1, 1), new DateOnly(2003, 1, 1), "Marco Antonio de Oliveira Maciel", "PSDB",
            GovernmentType.NotInformed, ArrivalMethod.DirectElection,
            "A página de vice-presidência da Biblioteca da Presidência registra o segundo mandato de 01/01/1999 a 01/01/2003, com eleição direta. A biografia registra a reeleição pela coligação PSDB/PFL/PTB/PPB.");

        vargas.RefreshSummary([vargasProvisional, estadoNovo, vargas1951]);
        jk.RefreshSummary([jkMandate]);
        fhc.RefreshSummary([fhcFirst, fhcSecond]);
        vargas.RebuildSearchText();
        jk.RebuildSearchText();
        fhc.RebuildSearchText();

        var estadoEvent = HistoricalEvent.Create("Dissolução do Congresso e início do Estado Novo");
        estadoEvent.President = vargas;
        estadoEvent.Presidency = estadoNovo;
        estadoEvent.EventDate = new DateOnly(1937, 11, 10);
        estadoEvent.EndDate = new DateOnly(1945, 10, 29);
        estadoEvent.EventType = EventType.Politics;
        estadoEvent.Importance = ImportanceLevel.Landmark;
        estadoEvent.SummaryKind = StatementKind.OfficialData;
        estadoEvent.NeutralSummary = "A biografia em inglês da Biblioteca da Presidência registra que, em 10 de novembro de 1937, Getúlio Vargas anunciou a dissolução do Congresso e o início do período denominado Estado Novo, e que deixou o cargo em 29 de outubro de 1945.";
        estadoEvent.EnsureInvariants();
        estadoEvent.RebuildSearchText();
        Publish(estadoEvent, DataProvenance.DemonstrationSeed);

        var law = Law.Create(NormKind.OrdinaryLaw, "8.080", 1990, "Dispõe sobre as condições para a promoção, proteção e recuperação da saúde, a organização e o funcionamento dos serviços correspondentes e dá outras providências.");
        law.Summary = "Ementa oficial reproduzida de forma curta. O preâmbulo da página do Portal da Legislação diz que o Congresso Nacional decreta e que o Presidente da República sanciona. Esta ficha não atribui autoria ao presidente em exercício.";
        law.FullTextUrl = lawPage.Url;
        law.SanctionDate = new DateOnly(1990, 9, 19);
        law.Origin = NormOrigin.Legislature;
        law.OperationalStatus = LawOperationalStatus.InForce;
        law.LegislativeProcessNote = "A data 19/09/1990 é a do epígrafe oficial. A página do Portal da Legislação apresenta o texto com marcas de alteração posterior; a vigência de cada dispositivo deve ser conferida ali. Nenhum presidente foi vinculado automaticamente.";
        law.AttributedAsAuthor = false;
        law.Slug = "lei-8080-1990";
        law.RebuildSearchText();
        Publish(law, DataProvenance.DemonstrationSeed);

        var policy = Policy.Create("Plano de Metas");
        policy.President = jk;
        policy.Presidency = jkMandate;
        policy.Description = "A biografia do Arquivo Nacional associa a administração de Juscelino Kubitschek ao Plano de Metas, descrito ali como projeto voltado à industrialização e à infraestrutura. Este cadastro não avalia resultados.";
        policy.RebuildSearchText();
        Publish(policy, DataProvenance.DemonstrationSeed);

        var policyStatement = SourcedStatement.Create(ContentEntityType.Policy, policy.Id, StatementKind.OfficialData,
            "A biografia do Arquivo Nacional associa a administração Kubitschek ao Plano de Metas e registra o lema “cinquenta anos de progresso em cinco anos de governo”.",
            "Arquivo Nacional — biografia de Juscelino Kubitschek de Oliveira");
        Publish(policyStatement, DataProvenance.DemonstrationSeed);

        var deathStatement = SourcedStatement.Create(ContentEntityType.President, vargas.Id, StatementKind.OfficialData,
            "A Biblioteca da Presidência registra falecimento em 24 de agosto de 1954, no Rio de Janeiro.",
            "Biblioteca da Presidência da República");
        Publish(deathStatement, DataProvenance.DemonstrationSeed);

        db.Sources.AddRange(vargasPage, vargasPdf, jkPage, jkArchive, fhcPage, fhcVice, lawPage);
        db.Presidents.AddRange(vargas, jk, fhc);
        db.Presidencies.AddRange(vargasProvisional, estadoNovo, vargas1951, jkMandate, fhcFirst, fhcSecond);
        db.HistoricalEvents.Add(estadoEvent);
        db.Laws.Add(law);
        db.Policies.Add(policy);
        db.Statements.AddRange(policyStatement, deathStatement);

        Link(db, vargasPage, ContentEntityType.President, vargas.Id, "Página consultada para nascimento, falecimento e governo provisório.");
        Link(db, vargasPdf, ContentEntityType.President, vargas.Id, "Página consultada para Estado Novo, saída em 1945 e posse em 1951.");
        Link(db, vargasPage, ContentEntityType.Presidency, vargasProvisional.Id, null);
        Link(db, vargasPdf, ContentEntityType.Presidency, estadoNovo.Id, null);
        Link(db, vargasPdf, ContentEntityType.Presidency, vargas1951.Id, null);
        Link(db, vargasPage, ContentEntityType.Presidency, vargas1951.Id, "Data de falecimento usada como limite final deste cadastro.");
        Link(db, vargasPage, ContentEntityType.SourcedStatement, deathStatement.Id, null);
        Link(db, jkPage, ContentEntityType.President, jk.Id, null);
        Link(db, jkArchive, ContentEntityType.President, jk.Id, null);
        Link(db, jkPage, ContentEntityType.Presidency, jkMandate.Id, null);
        Link(db, jkArchive, ContentEntityType.Presidency, jkMandate.Id, null);
        Link(db, fhcPage, ContentEntityType.President, fhc.Id, null);
        Link(db, fhcVice, ContentEntityType.President, fhc.Id, null);
        Link(db, fhcPage, ContentEntityType.Presidency, fhcFirst.Id, null);
        Link(db, fhcVice, ContentEntityType.Presidency, fhcFirst.Id, null);
        Link(db, fhcVice, ContentEntityType.Presidency, fhcSecond.Id, null);
        Link(db, fhcPage, ContentEntityType.Presidency, fhcSecond.Id, "A biografia registra a coligação da reeleição.");
        Link(db, vargasPdf, ContentEntityType.HistoricalEvent, estadoEvent.Id, null);
        Link(db, lawPage, ContentEntityType.Law, law.Id, "Texto oficial e ementa.");
        Link(db, jkArchive, ContentEntityType.Policy, policy.Id, null);
        Link(db, jkArchive, ContentEntityType.SourcedStatement, policyStatement.Id, null);

        var politica = categories.Single(category => category.Slug == "politica");
        var saude = categories.Single(category => category.Slug == "saude");
        var economia = categories.Single(category => category.Slug == "economia");
        var infraestrutura = categories.Single(category => category.Slug == "infraestrutura");
        db.CategoryLinks.AddRange(
            new ContentCategoryLink { EntityType = ContentEntityType.HistoricalEvent, EntityId = estadoEvent.Id, CategoryId = politica.Id, IsPrimary = true },
            new ContentCategoryLink { EntityType = ContentEntityType.Law, EntityId = law.Id, CategoryId = saude.Id, IsPrimary = true },
            new ContentCategoryLink { EntityType = ContentEntityType.Policy, EntityId = policy.Id, CategoryId = economia.Id, IsPrimary = true },
            new ContentCategoryLink { EntityType = ContentEntityType.Policy, EntityId = policy.Id, CategoryId = infraestrutura.Id, IsPrimary = false });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static List<Category> SeedCategories()
    {
        var names = new[]
        {
            "Economia", "Educação", "Saúde", "Segurança", "Infraestrutura", "Trabalho", "Previdência", "Assistência Social",
            "Meio Ambiente", "Ciência e Tecnologia", "Cultura", "Relações Internacionais", "Política", "Justiça",
            "Administração Pública", "Direitos Humanos", "Agricultura", "Indústria", "Energia", "Transportes",
            "Habitação", "Tributação", "Defesa", "Esportes", "Comunicação"
        };
        var categories = new List<Category>();
        for (var index = 0; index < names.Length; index++)
        {
            var category = Category.Create(names[index], "Categoria de organização do acervo. Não é, por si, uma afirmação histórica.");
            category.SortOrder = index + 1;
            Publish(category, DataProvenance.SystemTaxonomy);
            categories.Add(category);
        }
        return categories;
    }

    private static IEnumerable<Indicator> SeedIndicators()
    {
        (string Name, string Unit)[] items =
        [
            ("Inflação", "% a.a."),
            ("PIB", "unidade monetária"),
            ("Desemprego", "%"),
            ("Dívida pública", "% do PIB"),
            ("Salário mínimo", "moeda corrente"),
            ("Taxa de pobreza", "%"),
            ("Mortalidade infantil", "por mil nascidos vivos"),
            ("Escolarização", "%"),
            ("Produção industrial", "índice"),
            ("Exportações", "unidade monetária"),
            ("Importações", "unidade monetária")
        ];
        foreach (var item in items)
        {
            var indicator = Indicator.Create(item.Name, item.Unit, "Indicador preparado para comparação. O seed de demonstração não inclui valores numéricos.");
            Publish(indicator, DataProvenance.SystemTaxonomy);
            yield return indicator;
        }
    }

    private static SourceRecord Source(string name, string url, string publisher, SourceType type = SourceType.OfficialDocument)
    {
        var source = SourceRecord.Create(name, type, ReliabilityLevel.Primary, url);
        source.Publisher = publisher;
        source.Title = name;
        source.AccessedAt = AccessedOn;
        source.Notes = "Fonte consultada na carga inicial de demonstração em 07/10/2026. O registro ainda não passou por revisão editorial humana dentro do sistema.";
        Publish(source, DataProvenance.DemonstrationSeed);
        source.RebuildSearchText();
        return source;
    }

    private static Presidency Mandate(President president, int ordinal, DateOnly start, DateOnly end, string? vice, string? party, GovernmentType government, ArrivalMethod arrival, string context)
    {
        var mandate = new Presidency { President = president, Ordinal = ordinal, VicePresident = vice, Party = party, GovernmentType = government, ArrivalMethod = arrival, PoliticalContext = context };
        mandate.SetPeriod(start, end);
        mandate.RebuildSearchText(president.FullName);
        Publish(mandate, DataProvenance.DemonstrationSeed);
        return mandate;
    }

    private static void Publish(PublishableEntity entity, DataProvenance provenance)
    {
        entity.Status = PublicationStatus.Published;
        entity.PublishedAt = DateTimeOffset.UtcNow;
        entity.Provenance = provenance;
    }

    private static void Link(AppDbContext db, SourceRecord source, ContentEntityType type, Guid entityId, string? annotation) =>
        db.FactSources.Add(FactSource.Link(source.Id, type, entityId, null, annotation));
}
