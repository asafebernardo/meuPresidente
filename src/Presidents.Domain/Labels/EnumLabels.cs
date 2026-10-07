using Presidents.Domain.Enums;

namespace Presidents.Domain.Labels;

public static class EnumLabels
{
    public static string For(PublicationStatus status) => status switch
    {
        PublicationStatus.Draft => "Rascunho",
        PublicationStatus.Review => "Em revisão",
        PublicationStatus.Published => "Publicado",
        PublicationStatus.Archived => "Arquivado",
        _ => status.ToString()
    };

    public static string For(StatementKind kind) => kind switch
    {
        StatementKind.DocumentedFact => "Fato documentado",
        StatementKind.HistoricalInterpretation => "Interpretação histórica",
        StatementKind.Opinion => "Opinião",
        StatementKind.GroupCriticism => "Crítica de grupo",
        StatementKind.JournalisticInformation => "Informação jornalística",
        StatementKind.OfficialData => "Dado oficial",
        _ => kind.ToString()
    };

    public static string For(SourceType type) => type switch
    {
        SourceType.Government => "Governo",
        SourceType.Legislation => "Legislação",
        SourceType.Newspaper => "Jornal",
        SourceType.Academic => "Acadêmica",
        SourceType.Book => "Livro",
        SourceType.InternationalOrganization => "Organismo internacional",
        SourceType.OfficialDocument => "Documento oficial",
        SourceType.Other => "Outra",
        _ => type.ToString()
    };

    public static string For(ReliabilityLevel level) => level switch
    {
        ReliabilityLevel.Primary => "Primária",
        ReliabilityLevel.Secondary => "Secundária",
        ReliabilityLevel.Tertiary => "Terciária",
        _ => level.ToString()
    };

    public static string For(EventType type) => type switch
    {
        EventType.Politics => "Política",
        EventType.Economy => "Economia",
        EventType.War => "Guerra",
        EventType.Crisis => "Crise",
        EventType.Election => "Eleição",
        EventType.PublicPolicy => "Política pública",
        EventType.InternationalRelations => "Relações internacionais",
        EventType.Reform => "Reforma",
        EventType.Scandal => "Escândalo",
        EventType.Demonstration => "Manifestação",
        EventType.Disaster => "Desastre",
        EventType.Agreement => "Acordo",
        EventType.Infrastructure => "Infraestrutura",
        EventType.Other => "Outro",
        _ => type.ToString()
    };

    public static string For(ImportanceLevel level) => level switch
    {
        ImportanceLevel.Contextual => "Contextual",
        ImportanceLevel.Notable => "Relevante",
        ImportanceLevel.Major => "Alto",
        ImportanceLevel.Landmark => "Marco",
        _ => level.ToString()
    };

    public static string For(NormKind kind) => kind switch
    {
        NormKind.OrdinaryLaw => "Lei ordinária",
        NormKind.ComplementaryLaw => "Lei complementar",
        NormKind.ConstitutionalAmendment => "Emenda constitucional",
        NormKind.Decree => "Decreto",
        NormKind.DecreeLaw => "Decreto-lei",
        NormKind.ProvisionalMeasure => "Medida provisória",
        NormKind.Ordinance => "Portaria",
        NormKind.Constitution => "Constituição",
        NormKind.Other => "Outro",
        _ => kind.ToString()
    };

    public static string For(LawOperationalStatus status) => status switch
    {
        LawOperationalStatus.Unknown => "Situação não informada",
        LawOperationalStatus.InForce => "Vigente",
        LawOperationalStatus.Revoked => "Revogada",
        LawOperationalStatus.PartiallyRevoked => "Parcialmente revogada",
        LawOperationalStatus.NotInForce => "Não vigente",
        LawOperationalStatus.Suspended => "Suspensa",
        _ => status.ToString()
    };

    public static string For(NormOrigin origin) => origin switch
    {
        NormOrigin.Unknown => "Origem não informada",
        NormOrigin.Executive => "Poder Executivo",
        NormOrigin.Legislature => "Congresso Nacional",
        NormOrigin.ConstituentAssembly => "Assembleia Constituinte",
        NormOrigin.PopularInitiative => "Iniciativa popular",
        NormOrigin.Judiciary => "Poder Judiciário",
        NormOrigin.Other => "Outra",
        _ => origin.ToString()
    };

    public static string For(GovernmentType type) => type switch
    {
        GovernmentType.NotInformed => "Não informado",
        GovernmentType.Provisional => "Governo provisório",
        GovernmentType.Constitutional => "Governo constitucional",
        GovernmentType.EstadoNovo => "Estado Novo",
        GovernmentType.Parliamentary => "Parlamentarismo",
        GovernmentType.MilitaryRegime => "Regime militar",
        GovernmentType.Other => "Outro",
        _ => type.ToString()
    };

    public static string For(ArrivalMethod method) => method switch
    {
        ArrivalMethod.NotInformed => "Não informado",
        ArrivalMethod.DirectElection => "Eleição direta",
        ArrivalMethod.IndirectElection => "Eleição indireta",
        ArrivalMethod.VicePresidentialSuccession => "Sucessão do vice-presidente",
        ArrivalMethod.ConstitutionalSuccession => "Sucessão constitucional",
        ArrivalMethod.ProvisionalGovernment => "Governo provisório",
        ArrivalMethod.InstitutionalRupture => "Ruptura institucional",
        ArrivalMethod.Other => "Outra",
        _ => method.ToString()
    };

    public static string For(DataProvenance provenance) => provenance switch
    {
        DataProvenance.Editorial => "Revisão editorial",
        DataProvenance.DemonstrationSeed => "Demonstração",
        DataProvenance.Imported => "Importado",
        DataProvenance.SystemTaxonomy => "Taxonomia do sistema",
        _ => provenance.ToString()
    };

    public static string For(ImportChannel channel) => channel switch
    {
        ImportChannel.Legislation => "Legislação oficial",
        ImportChannel.News => "Notícia",
        ImportChannel.Academic => "Fonte acadêmica",
        _ => channel.ToString()
    };

    public static string Any(Enum value) => value switch
    {
        PublicationStatus status => For(status),
        StatementKind kind => For(kind),
        SourceType type => For(type),
        ReliabilityLevel level => For(level),
        EventType eventType => For(eventType),
        ImportanceLevel importance => For(importance),
        NormKind norm => For(norm),
        LawOperationalStatus lawStatus => For(lawStatus),
        NormOrigin origin => For(origin),
        GovernmentType government => For(government),
        ArrivalMethod arrival => For(arrival),
        DataProvenance provenance => For(provenance),
        ImportChannel channel => For(channel),
        ContentEntityType entity => entity switch
        {
            ContentEntityType.President => "Presidente",
            ContentEntityType.Presidency => "Mandato",
            ContentEntityType.HistoricalEvent => "Acontecimento",
            ContentEntityType.Law => "Lei",
            ContentEntityType.Policy => "Política",
            ContentEntityType.Indicator => "Indicador",
            ContentEntityType.PresidentIndicator => "Valor de indicador",
            ContentEntityType.SourcedStatement => "Afirmação",
            ContentEntityType.Category => "Categoria",
            ContentEntityType.Source => "Fonte",
            _ => entity.ToString()
        },
        _ => value.ToString()
    };
}
