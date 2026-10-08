using Presidents.Domain.Common;
using Presidents.Domain.Enums;
using Presidents.Domain.Rules;

namespace Presidents.Domain.Entities;

/// <summary>
/// Um período no poder. Um presidente pode ter vários, e eles não são tratados como equivalentes.
/// </summary>
public sealed class Presidency : PublishableEntity
{
    public Guid PresidentId { get; set; }
    public President? President { get; set; }
    public int Ordinal { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? VicePresident { get; set; }
    public string? Party { get; set; }
    public string? PoliticalContext { get; set; }
    public GovernmentType GovernmentType { get; set; } = GovernmentType.NotInformed;
    public ArrivalMethod ArrivalMethod { get; set; } = ArrivalMethod.NotInformed;
    public string? DivergenceNote { get; set; }
    public string SearchText { get; set; } = string.Empty;

    public void SetPeriod(DateOnly start, DateOnly? end)
    {
        DateRules.EnsurePeriod(start, end, "mandato");
        StartDate = start;
        EndDate = end;
    }

    public void RebuildSearchText(string? presidentName = null)
    {
        SearchText = TextNormalizer.Fold(string.Join(' ',
            presidentName, Party, VicePresident, PoliticalContext, GovernmentType, ArrivalMethod));
    }
}
