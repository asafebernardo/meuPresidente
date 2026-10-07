using Presidents.Application.Comparison;
using Presidents.Application.Rag;
using Presidents.Application.Search;
using Presidents.Application.Security;
using Presidents.Domain.Common;
using Presidents.Domain.Entities;
using Presidents.Domain.Enums;
using Presidents.Domain.Rules;

namespace Presidents.UnitTests;

public sealed class PresidentCreationTests
{
    [Fact]
    public void Create_assigns_slug_and_leaves_dates_empty()
    {
        var president = President.Create("JK", "Juscelino Kubitschek de Oliveira", "juscelino-kubitschek");

        Assert.Equal("juscelino-kubitschek", president.Slug);
        Assert.Null(president.StartDate);
        Assert.Equal(PublicationStatus.Draft, president.Status);
    }

    [Fact]
    public void Create_rejects_blank_name()
    {
        var error = Assert.Throws<DomainException>(() => President.Create(" ", "Nome completo"));
        Assert.Contains("nome", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DateValidationTests
{
    [Fact]
    public void Death_cannot_precede_birth()
    {
        var error = Assert.Throws<DomainException>(() => DateRules.EnsureBirthDeath(new DateOnly(1900, 1, 1), new DateOnly(1899, 1, 1)));
        Assert.Contains("falecimento", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Mandate_cannot_start_before_the_republic()
    {
        var error = Assert.Throws<DomainException>(() => DateRules.EnsurePeriod(new DateOnly(1800, 1, 1), new DateOnly(1801, 1, 1), "mandato"));
        Assert.Contains("1889", error.Message);
    }

    [Fact]
    public void End_requires_start()
    {
        Assert.Throws<DomainException>(() => DateRules.EnsurePeriod(null, new DateOnly(1956, 1, 31)));
    }
}

public sealed class LawCreationTests
{
    [Fact]
    public void Create_does_not_attribute_authorship()
    {
        var law = Law.Create(NormKind.OrdinaryLaw, "8.080", 1990, "Ementa de teste");

        Assert.False(law.AttributedAsAuthor);
        Assert.Null(law.Proposer);
        Assert.Equal("8080", law.NumberNormalized);
    }

    [Fact]
    public void Authorship_requires_a_proposer()
    {
        var law = Law.Create(NormKind.Decree, "1", 1930, "Ato de teste");
        Assert.Throws<DomainException>(() => law.AttributeAuthorship(" "));
        law.AttributeAuthorship("Congresso Nacional");
        Assert.True(law.AttributedAsAuthor);
        law.ClearAuthorship();
        Assert.False(law.AttributedAsAuthor);
    }
}

public sealed class SourceAssociationTests
{
    [Fact]
    public void Link_keeps_a_short_excerpt()
    {
        var link = FactSource.Link(Guid.CreateVersion7(), ContentEntityType.Law, Guid.CreateVersion7(), "Trecho curto.", null);
        Assert.Equal("Trecho curto.", link.Excerpt);
    }

    [Fact]
    public void Link_rejects_an_excerpt_above_the_limit()
    {
        var excerpt = new string('a', FieldLimits.Excerpt + 1);
        var error = Assert.Throws<DomainException>(() => FactSource.Link(Guid.CreateVersion7(), ContentEntityType.Law, Guid.CreateVersion7(), excerpt, null));
        Assert.Contains("400", error.Message);
    }
}

public sealed class SearchTests
{
    [Fact]
    public void Grouper_separates_hits_by_kind()
    {
        var response = SearchGrouper.Group("educação",
        [
            Hit(SearchHitKind.President, "Presidente"),
            Hit(SearchHitKind.Law, "Lei"),
            Hit(SearchHitKind.Event, "Acontecimento"),
            Hit(SearchHitKind.Policy, "Política"),
            Hit(SearchHitKind.Source, "Fonte")
        ]);

        Assert.Equal("educação", response.Query);
        Assert.Single(response.Presidents);
        Assert.Single(response.Laws);
        Assert.Single(response.Events);
        Assert.Single(response.Policies);
        Assert.Single(response.Sources);
        Assert.Equal(5, response.Total);
    }

    private static SearchHit Hit(SearchHitKind kind, string title) =>
        new(Guid.CreateVersion7(), kind, title, null, "/" + title, null);
}

public sealed class ComparisonTests
{
    [Fact]
    public void Assemble_preserves_order_and_does_not_rank()
    {
        var result = ComparisonAssembler.Assemble(
        [
            Slice("Segundo"),
            Slice("Primeiro")
        ]);

        Assert.Equal(ComparisonAssembler.Disclaimer, result.Disclaimer);
        Assert.Equal(["Segundo", "Primeiro"], result.Columns.Select(column => column.Name));
    }

    [Fact]
    public void Assemble_rejects_more_than_four_presidents()
    {
        var slices = Enumerable.Range(1, 5).Select(index => Slice($"P{index}")).ToList();
        var error = Assert.Throws<DomainException>(() => ComparisonAssembler.Assemble(slices));
        Assert.Contains("4", error.Message);
    }

    [Fact]
    public void Missing_indicator_is_explicit()
    {
        var withValue = Slice("Com valor");
        withValue.IndicatorValues["Inflação"] = [(1956, 12.5m, "%")];
        var result = ComparisonAssembler.Assemble([withValue, Slice("Sem valor")]);
        var row = Assert.Single(result.Indicators);
        Assert.Equal($"1956: {12.5m}", row.Values[0]);
        Assert.Equal("Sem valor publicado", row.Values[1]);
    }

    private static PresidentSlice Slice(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = name,
        Slug = name.ToLowerInvariant(),
        Mandate = "01/01/1956 – 31/01/1961"
    };
}

public sealed class PermissionTests
{
    [Fact]
    public void Anonymous_cannot_edit()
    {
        Assert.Throws<ForbiddenException>(() => ContentPermissions.EnsureCanEdit(ActorContext.Anonymous));
    }

    [Fact]
    public void Editor_can_send_to_review_but_cannot_publish()
    {
        var editor = new ActorContext("editor", [AppRoles.Editor]);
        PublicationWorkflow.Ensure(PublicationStatus.Draft, PublicationStatus.Review, editor);
        var error = Assert.Throws<DomainException>(() => PublicationWorkflow.Ensure(PublicationStatus.Review, PublicationStatus.Published, editor));
        Assert.Contains("não é permitida", error.Message);
    }

    [Fact]
    public void Reviewer_can_publish_and_unpublish()
    {
        var reviewer = new ActorContext("reviewer", [AppRoles.Reviewer]);
        PublicationWorkflow.Ensure(PublicationStatus.Review, PublicationStatus.Published, reviewer);
        PublicationWorkflow.Ensure(PublicationStatus.Published, PublicationStatus.Draft, reviewer);
    }

    [Fact]
    public void Publishing_a_fact_requires_a_source()
    {
        var admin = new ActorContext("admin", [AppRoles.Admin]);
        var error = Assert.Throws<DomainException>(() => PublicationWorkflow.Apply(President.Create("A", "Nome A"), PublicationStatus.Published, admin, ContentEntityType.President, 0));
        Assert.Contains("fonte", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class AnswerComposerTests
{
    [Fact]
    public void Empty_pack_uses_the_insufficient_evidence_sentence()
    {
        var answer = HistoricalAnswerComposer.Compose(new EvidencePack([]), null);
        Assert.True(answer.InsufficientEvidence);
        Assert.Equal(HistoricalAnswerComposer.InsufficientEvidence, answer.Text);
        Assert.Empty(answer.Citations);
    }

    [Fact]
    public void Unknown_source_selection_is_not_turned_into_a_citation()
    {
        var known = Guid.CreateVersion7();
        var pack = new EvidencePack(
        [
            new EvidenceItem(known, "Afirmação armazenada.", "Fonte", "https://exemplo.gov.br/doc", new DateOnly(1954, 8, 24), ReliabilityLevel.Primary, "Primária")
        ]);

        var answer = HistoricalAnswerComposer.Compose(pack, [Guid.CreateVersion7()]);
        Assert.True(answer.InsufficientEvidence);
        Assert.Empty(answer.Citations);
    }

    [Fact]
    public void Selected_citation_keeps_the_stored_statement_and_url()
    {
        var known = Guid.CreateVersion7();
        var pack = new EvidencePack(
        [
            new EvidenceItem(known, "Afirmação armazenada.", "Fonte", "https://exemplo.gov.br/doc", new DateOnly(1954, 8, 24), ReliabilityLevel.Primary, "Primária")
        ]);

        var answer = HistoricalAnswerComposer.Compose(pack, [known]);
        var citation = Assert.Single(answer.Citations);
        Assert.Equal("Afirmação armazenada.", citation.Statement);
        Assert.Equal("https://exemplo.gov.br/doc", citation.Url);
        Assert.Equal("Primária", citation.Reliability);
    }
}

public sealed class IngestionDedupTests
{
    [Fact]
    public void Same_hash_url_or_external_id_is_a_duplicate_inside_one_adapter()
    {
        var existing = new[] { new IngestionFingerprint("GovernmentLegislationImporter", "abc", "https://www.planalto.gov.br/lei", "ext-1") };
        Assert.True(IngestionDedup.IsDuplicate(existing, new IngestionFingerprint("GovernmentLegislationImporter", "abc", null, null)));
        Assert.True(IngestionDedup.IsDuplicate(existing, new IngestionFingerprint("GovernmentLegislationImporter", "outro", "https://www.planalto.gov.br/lei", null)));
        Assert.True(IngestionDedup.IsDuplicate(existing, new IngestionFingerprint("GovernmentLegislationImporter", "outro", null, "ext-1")));
        Assert.False(IngestionDedup.IsDuplicate(existing, new IngestionFingerprint("NewsImporter", "abc", "https://www.planalto.gov.br/lei", "ext-1")));
    }
}
