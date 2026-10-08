using System.Globalization;

namespace Presidents.Domain.Catalog;

public static class PeriodMetrics
{
    public const string Disclaimer = "A variação descreve o período do mandato segundo a fonte oficial. Ela não isola o efeito das leis sancionadas nesse intervalo.";

    private static readonly CultureInfo Brazilian = CultureInfo.GetCultureInfo("pt-BR");

    public static decimal Compound(IReadOnlyList<decimal> monthlyPercents)
    {
        decimal factor = 1m;
        foreach (var percent in monthlyPercents)
            factor *= 1m + percent / 100m;
        return (factor - 1m) * 100m;
    }

    public static string AccumulatedInflation(IReadOnlyList<decimal> monthlyPercents)
    {
        if (monthlyPercents.Count == 0)
            return "Nenhum mês de IPCA tem o primeiro dia dentro deste mandato.";

        return $"IPCA acumulado em {monthlyPercents.Count} meses com início no mandato: {Format(Compound(monthlyPercents))}%.";
    }

    public static string AverageAnnualChange(IReadOnlyList<decimal> annualPercents, string seriesSpan)
    {
        if (annualPercents.Count == 0)
            return $"Nenhum ano civil completo deste mandato está na série ({seriesSpan}).";

        var average = annualPercents.Average();
        return $"Média da variação anual em {annualPercents.Count} anos civis inteiros no mandato ({seriesSpan}): {Format(average)}%.";
    }

    public static string UnemploymentSpan(decimal first, decimal last)
    {
        var delta = last - first;
        var direction = delta switch
        {
            > 0 => "alta",
            < 0 => "queda",
            _ => "variação"
        };
        return $"Taxa de desocupação de {Format(first)}% para {Format(last)}% ({direction} de {Format(Math.Abs(delta))} ponto percentual) entre o primeiro e o último trimestre com início no mandato.";
    }

    public static string NominalWage(DateOnly firstDate, decimal first, DateOnly lastDate, decimal last)
    {
        if (firstDate < MandateCoverage.RealPlan || lastDate < MandateCoverage.RealPlan)
            return $"Extremos nominais da série: {FormatMoney(first)} em {firstDate:dd/MM/yyyy} e {FormatMoney(last)} em {lastDate:dd/MM/yyyy}. A série atravessa mudança de padrão monetário, então não há variação percentual.";

        if (first == 0)
            return $"Salário mínimo nominal de {FormatMoney(first)} para {FormatMoney(last)}.";

        var change = (last - first) / first * 100m;
        return $"Salário mínimo nominal de {FormatMoney(first)} ({firstDate:dd/MM/yyyy}) para {FormatMoney(last)} ({lastDate:dd/MM/yyyy}), variação de {Format(change)}% na moeda corrente.";
    }

    public static string Format(decimal value) => value.ToString("N1", Brazilian);

    private static string FormatMoney(decimal value) => value.ToString("N2", Brazilian);
}
