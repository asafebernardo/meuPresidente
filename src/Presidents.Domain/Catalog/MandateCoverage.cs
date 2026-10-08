using Presidents.Domain.Entities;

namespace Presidents.Domain.Catalog;

public static class MandateCoverage
{
    public static readonly DateOnly Redemocratization = new(1985, 3, 15);
    public static readonly DateOnly RealPlan = new(1994, 7, 1);

    public static Presidency? Find(IReadOnlyList<Presidency> mandates, DateOnly date)
    {
        Presidency? chosen = null;
        foreach (var mandate in mandates)
        {
            if (date < mandate.StartDate)
                continue;
            if (mandate.EndDate is { } end && date > end)
                continue;
            if (chosen is null || mandate.StartDate > chosen.StartDate)
                chosen = mandate;
        }

        return chosen;
    }

    public static bool CoversFullYear(IReadOnlyList<Presidency> mandates, Presidency mandate, int year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        return Find(mandates, start)?.Id == mandate.Id && Find(mandates, end)?.Id == mandate.Id;
    }
}
