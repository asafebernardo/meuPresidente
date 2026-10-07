using FluentValidation;
using Presidents.Application.Contracts;
using Presidents.Domain.Common;

namespace Presidents.Application.Validation;

public static class UrlRules
{
    public static IRuleBuilderOptions<T, string?> HttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) ||
                           (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            .WithMessage("Informe uma URL http ou https válida.");
}

public sealed class PresidentFormValidator : AbstractValidator<PresidentForm>
{
    public PresidentFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Name);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Slug).MaximumLength(FieldLimits.Slug);
        RuleFor(x => x.Biography).MaximumLength(FieldLimits.LongText);
        RuleFor(x => x.HistoricalContext).MaximumLength(FieldLimits.LongText);
        RuleFor(x => x.Party).MaximumLength(FieldLimits.Party);
        RuleFor(x => x.VicePresident).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.PhotoUrl).HttpUrl().MaximumLength(FieldLimits.Url);
        RuleFor(x => x.WikipediaUrl).HttpUrl().MaximumLength(FieldLimits.Url);
        RuleFor(x => x.OfficialBiographyUrl).HttpUrl().MaximumLength(FieldLimits.Url);
    }
}

public sealed class PresidencyFormValidator : AbstractValidator<PresidencyForm>
{
    public PresidencyFormValidator()
    {
        RuleFor(x => x.PresidentId).NotEmpty();
        RuleFor(x => x.Ordinal).InclusiveBetween(1, 30);
        RuleFor(x => x.Party).MaximumLength(FieldLimits.Party);
        RuleFor(x => x.VicePresident).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.PoliticalContext).MaximumLength(FieldLimits.LongText);
        RuleFor(x => x.DivergenceNote).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class CategoryFormValidator : AbstractValidator<CategoryForm>
{
    public CategoryFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Slug).MaximumLength(FieldLimits.Slug);
        RuleFor(x => x.Description).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class EventFormValidator : AbstractValidator<EventForm>
{
    public EventFormValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Description).MaximumLength(FieldLimits.LongText);
        RuleFor(x => x.NeutralSummary).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.DivergenceNote).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class LawFormValidator : AbstractValidator<LawForm>
{
    public LawFormValidator()
    {
        RuleFor(x => x.Number).NotEmpty().MaximumLength(40);
        RuleFor(x => x.Year).InclusiveBetween(1800, 2100);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Summary).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.FullTextUrl).HttpUrl().MaximumLength(FieldLimits.Url);
        RuleFor(x => x.Proposer).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.LegislativeProcessNote).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.VetoNote).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.DivergenceNote).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x).Must(x => !x.AttributeAuthorship || !string.IsNullOrWhiteSpace(x.Proposer))
            .WithMessage("Marcar autoria exige o nome do autor ou proponente.");
    }
}

public sealed class PolicyFormValidator : AbstractValidator<PolicyForm>
{
    public PolicyFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Description).MaximumLength(FieldLimits.LongText);
        RuleFor(x => x.Objective).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class SourceFormValidator : AbstractValidator<SourceForm>
{
    public SourceFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Url).HttpUrl().MaximumLength(FieldLimits.Url);
        RuleFor(x => x.Publisher).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Author).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Title).MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Notes).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class IndicatorFormValidator : AbstractValidator<IndicatorForm>
{
    public IndicatorFormValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(FieldLimits.Unit);
        RuleFor(x => x.Description).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class IndicatorValueFormValidator : AbstractValidator<IndicatorValueForm>
{
    public IndicatorValueFormValidator()
    {
        RuleFor(x => x.PresidentId).NotEmpty();
        RuleFor(x => x.IndicatorId).NotEmpty();
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(1889, 2100);
        RuleFor(x => x.Note).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class StatementFormValidator : AbstractValidator<StatementForm>
{
    public StatementFormValidator()
    {
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.Attribution).MaximumLength(FieldLimits.Name);
    }
}

public sealed class FactSourceFormValidator : AbstractValidator<FactSourceForm>
{
    public FactSourceFormValidator()
    {
        RuleFor(x => x.SourceId).NotEmpty();
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.Excerpt).MaximumLength(FieldLimits.Excerpt);
        RuleFor(x => x.Annotation).MaximumLength(FieldLimits.Summary);
    }
}

public sealed class ImportRequestValidator : AbstractValidator<ImportRequest>
{
    public ImportRequestValidator()
    {
        RuleFor(x => x.Url).HttpUrl().MaximumLength(FieldLimits.Url);
        RuleFor(x => x.Title).MaximumLength(FieldLimits.Title);
        RuleFor(x => x.Summary).MaximumLength(FieldLimits.Summary);
        RuleFor(x => x.Excerpt).MaximumLength(FieldLimits.Excerpt);
        RuleFor(x => x.ExternalId).MaximumLength(200);
        RuleFor(x => x.Publisher).MaximumLength(FieldLimits.Name);
        RuleFor(x => x.Author).MaximumLength(FieldLimits.Name);
    }
}
