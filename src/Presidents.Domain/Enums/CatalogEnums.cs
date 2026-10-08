namespace Presidents.Domain.Enums;

public enum PublicationStatus
{
    Draft = 0,
    Review = 1,
    Published = 2,
    Archived = 3
}

public enum DataProvenance
{
    Editorial = 0,
    DemonstrationSeed = 1,
    Imported = 2,
    SystemTaxonomy = 3
}

public enum StatementKind
{
    DocumentedFact = 1,
    HistoricalInterpretation = 2,
    Opinion = 3,
    GroupCriticism = 4,
    JournalisticInformation = 5,
    OfficialData = 6
}

public enum SourceType
{
    Government = 1,
    Legislation = 2,
    Newspaper = 3,
    Academic = 4,
    Book = 5,
    InternationalOrganization = 6,
    OfficialDocument = 7,
    Other = 8
}

public enum ReliabilityLevel
{
    Primary = 1,
    Secondary = 2,
    Tertiary = 3
}

public enum EventType
{
    Politics = 1,
    Economy = 2,
    War = 3,
    Crisis = 4,
    Election = 5,
    PublicPolicy = 6,
    InternationalRelations = 7,
    Reform = 8,
    Scandal = 9,
    Demonstration = 10,
    Disaster = 11,
    Agreement = 12,
    Infrastructure = 13,
    Other = 14
}

public enum ImportanceLevel
{
    Contextual = 1,
    Notable = 2,
    Major = 3,
    Landmark = 4
}

public enum NormKind
{
    OrdinaryLaw = 1,
    ComplementaryLaw = 2,
    ConstitutionalAmendment = 3,
    Decree = 4,
    DecreeLaw = 5,
    ProvisionalMeasure = 6,
    Ordinance = 7,
    Constitution = 8,
    Other = 9
}

public enum LawOperationalStatus
{
    Unknown = 0,
    InForce = 1,
    Revoked = 2,
    PartiallyRevoked = 3,
    NotInForce = 4,
    Suspended = 5
}

public enum NormOrigin
{
    Unknown = 0,
    Executive = 1,
    Legislature = 2,
    ConstituentAssembly = 3,
    PopularInitiative = 4,
    Judiciary = 5,
    Other = 6
}

public enum GovernmentType
{
    NotInformed = 0,
    Provisional = 1,
    Constitutional = 2,
    EstadoNovo = 3,
    Parliamentary = 4,
    MilitaryRegime = 5,
    Other = 6
}

public enum ArrivalMethod
{
    NotInformed = 0,
    DirectElection = 1,
    IndirectElection = 2,
    VicePresidentialSuccession = 3,
    ConstitutionalSuccession = 4,
    ProvisionalGovernment = 5,
    InstitutionalRupture = 6,
    Other = 7
}

public enum ContentEntityType
{
    President = 1,
    Presidency = 2,
    HistoricalEvent = 3,
    Law = 4,
    Policy = 5,
    Indicator = 6,
    PresidentIndicator = 7,
    SourcedStatement = 8,
    Category = 9,
    Source = 10
}

public enum ImportChannel
{
    Legislation = 1,
    News = 2,
    Academic = 3
}
